﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Threading;
using IBApi;
using log4net;
using log4net.Repository.Hierarchy;
using TNS.API.ApiDataObjects;

namespace TNS.API.IBApiWrapper
{
    public static class IBExtensions
    {
        private static readonly ILog Logger = LogManager.GetLogger(typeof(IBExtensions));
        public static ContractBase ToContract(this Contract contract)
        {
            switch (contract.SecType)
            {
                case "OPT":
                    // var optionType = contract.Right == "C" ? OptionType.Call : OptionType.Put;
                    OptionType optionType;
                    switch (contract.Right)
                    {
                        case "C":
                            optionType = OptionType.Call;
                            break;
                        case "P":
                            optionType = OptionType.Put;
                            break;
                        default:
                            optionType = OptionType.None;
                            break;
                    }
                    return new OptionContract(contract.Symbol, contract.Strike, GetExpiryDate(contract.Expiry), optionType,
                        contract.Exchange, Convert.ToInt32(contract.Multiplier), contract.Currency);
                case "STK":
                    return new SecurityContract(contract.Symbol, SecurityType.Stock);
                case "IND":
                    return new SecurityContract(contract.Symbol, SecurityType.Index);
                default:
                    throw new Exception("Invalid contract type received " +  contract.SecType);
            }
     }

        public static SecurityType ToSecurityType(string secType)
        {
            switch (secType)
            {
                case "OPT":
                    return SecurityType.Option;
                case "STK":
                    return SecurityType.Stock;
                case "IND":
                    return SecurityType.Option;
                default:
                    throw new Exception("Invalid contract type received " + secType);
            }
        }
      
        private static DateTime GetExpiryDate(string expDateStr)
        {
            if (string.IsNullOrEmpty(expDateStr))
                return DateTime.MinValue;
            DateTime expiryDate = DateTime.ParseExact(expDateStr, "yyyyMMdd", 
                        CultureInfo.CurrentCulture, DateTimeStyles.None);
            return expiryDate;
        }
        public static string GetSecType(SecurityType type)
        {
            switch (type)
            {
                case SecurityType.Stock:
                    return "STK";
                case SecurityType.Option:
                    return "OPT";
                case SecurityType.Index:
                    return "IND";
                default:
                    return string.Empty;
            }
        }
       
        public static Contract ToIbMainSecurityContract(this ContractBase msContract)
        {
            return new Contract
            {
                Symbol = msContract.Symbol,
                Currency = msContract.Currency,
                SecType = GetSecType(msContract.SecurityType),
                Exchange = msContract.Exchange
            };
        }
        public static Order ToIbOrder(this OrderData order, string mainAccount, string orderId)
        {
            var ibOrder = new Order
            {
                OrderId = Convert.ToInt32(orderId),
                Action = order.OrderAction == OrderAction.BUY ? "BUY" : "SELL",
                OrderType = order.OrderType.ToString().ToUpper(), //"LMT", MKT "MKT PRT" - not suported
                LmtPrice = order.LimitPrice,
                TotalQuantity = order.Quantity,
                Account = mainAccount,
                Tif = "DAY",
                Transmit = true,
                OrderRef = "IB-AutoTrader-Raz",
                WhatIf = order.WhatIf
            };
            return ibOrder;
            
        }

        public static OrderData ToOrderData(this Order order)
        {
            return new OrderData()
            {
                OrderType = (OrderType) Enum.Parse(typeof (OrderType), order.OrderType),
                OrderAction = (OrderAction) Enum.Parse(typeof (OrderAction), order.Action),
                LimitPrice = order.LmtPrice,
                Quantity = order.TotalQuantity,
                OrderId = order.OrderId.ToString()
            };
        }

        public static ContractDetailsData ToContractDetailsData(this ContractDetails contractDetails)
        {
            var contractBase = contractDetails.Summary.ToContract();
            var contractDetailsData = new ContractDetailsData(contractBase.Symbol,
                contractBase.SecurityType, contractBase.Exchange, contractBase.Currency);

            CalculateWorkTime(contractDetails, contractDetailsData);

            return contractDetailsData;
        }
        private static void CalculateWorkTime(ContractDetails contractDetails, 
            ContractDetailsData contractDetailsData)
        {
            //LiquidHours="20150427:0930-1600;20150428:0930-1600" 
            //"20090507:0700-1830,1830-2330;20090508:CLOSED." "20150503:CLOSED;20150504:0930-1600"
            //TimeZoneId="EST5EDT"

            string liquidHoursStr = contractDetails.LiquidHours;
            if (string.IsNullOrEmpty(liquidHoursStr))
            {
                Debug.Assert(false, "Problem with AAPLContractDetailsData " + 
                    "liquidHoursStr is null or empty!");
            }
            //For testing only:
            //liquidHoursStr = "20150624:0141-0600;20150600:0930-1600";//For Testing only
            string[] workingDays = liquidHoursStr.Split(';');

            //Eastern Standard Time
            TimeZoneInfo ist = TimeZoneInfo.FindSystemTimeZoneById("Israel Standard Time");
            //Israel Standard Time
            TimeZoneInfo est = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");

            string closedStr = workingDays[0].Substring(9, 6);

            contractDetailsData.IsWorkingDay = closedStr.Equals("CLOSED") == false;

            contractDetailsData.NextWorkingDay = DateTime.ParseExact(workingDays[1].Substring(0, 13),
                "yyyyMMdd:HHmm", CultureInfo.CurrentCulture, DateTimeStyles.None);

            contractDetailsData.NextWorkingDay = TimeZoneInfo.ConvertTime
                (contractDetailsData.NextWorkingDay, est, ist);
            if (!contractDetailsData.IsWorkingDay) return;

            contractDetailsData.StartTradingTime = DateTime.ParseExact(workingDays[0].Substring(0, 13),
                "yyyyMMdd:HHmm",CultureInfo.CurrentCulture, DateTimeStyles.None);

            contractDetailsData.StartTradingTimeLocal = TimeZoneInfo.ConvertTime
                (contractDetailsData.StartTradingTime, est, ist);
            //For Test:  StartTradingTimeLocal = DateTime.Now.AddMinutes(1);
            string endTimeStr = workingDays[0].Substring(0, 8) + " " + workingDays[0].Substring(14, 4);
            contractDetailsData.EndTradingTime = DateTime.ParseExact(endTimeStr, "yyyyMMdd HHmm",
                CultureInfo.CurrentCulture, DateTimeStyles.None);
            contractDetailsData.EndTradingTimeLocal = TimeZoneInfo.ConvertTime
                (contractDetailsData.EndTradingTime, est, ist);
            //For Test:  EndTradingTimeLocal = DateTime.Now.AddMinutes(2);
            string msg;
            if (contractDetailsData.IsWorkingDay == false)
            {
                msg = string.Format("Today is not workingDay. NextWorkingDay={0}", 
                    contractDetailsData.NextWorkingDay);
            }
            else
                msg = string.Format("Today is workingDay! StartTradingTime={0}, EndTradingTime:{1}. ",
                    contractDetailsData.StartTradingTimeLocal, contractDetailsData.EndTradingTimeLocal);
            Logger.Info(msg);
           
        }
    }
}