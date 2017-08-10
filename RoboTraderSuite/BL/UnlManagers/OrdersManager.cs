﻿using System;
using System.Collections.Generic;
using System.Linq;
using Infra.Bus;
using Infra.Enum;
using TNS.API;
using TNS.API.ApiDataObjects;
using TNS.BL.DataObjects;
using TNS.BL.Interfaces;

namespace TNS.BL.UnlManagers
{
    public class OrdersManager : UnlMemberBaseManager, IOrdersManager
    {
        //public event Action<OrderStatusData> OrderStatusDataUpdated;
        public OrdersManager(ITradingApi apiWrapper, ManagedSecurity managedSecurity, UNLManager unlManager) : base(apiWrapper, managedSecurity, unlManager)
        {
            OrderStatusDataDic = new Dictionary<string, OrderStatusData>();
            OptionNegotiatorDic = new Dictionary<string, OptionNegotiator>();
        }
        /// <summary>
        ///  = OrderType.LMT, used for testing
        /// </summary>
        private OrderType DefaultOrderType { get; set; } = OrderType.LMT;

        public override bool HandleMessage(IMessage message)
        {
            bool result = base.HandleMessage(message);
            
            if (result)
                return true;

            switch (message.APIDataType)
            {
                case EapiDataTypes.AccountSummaryData:
                    AccountSummaryData = message as AccountSummaryData;
                    return true;
                case EapiDataTypes.OrderStatus:
                    var orderStatusData = (OrderStatusData) message;
                    
                    OrderStatusDataDic[orderStatusData.OrderId] = orderStatusData;
                    
                    if ((orderStatusData.OrderStatus == OrderStatus.Filled) && (orderStatusData.WhatIf))
                    {
                        orderStatusData.Margin = orderStatusData.MaintMargin - this.AccountSummaryData.FullMaintMarginReq;

                        if (OptionNegotiatorDic.ContainsKey(orderStatusData.OrderId) == false)
                        {
                            var associatedNegotioator =
                            OptionNegotiatorDic.Values.FirstOrDefault(
                                neg => neg.WhatIfOrderId == orderStatusData.OrderId);
                            if (associatedNegotioator != null) associatedNegotioator.Margin = orderStatusData.Margin;
                        }
                        else//If it's true==> now isn't working time!==> terminate
                        {
                            SendOrderTaskAccomplished(orderStatusData.OrderId);
                        }
                        return true;
                    } 
                    if (OptionNegotiatorDic.ContainsKey(orderStatusData.OrderId))
                        OptionNegotiatorDic[orderStatusData.OrderId].SetOrderStatusData(orderStatusData);
                       
                    result = true;
                    break;
                case EapiDataTypes.OrderData:
                    result = true;
                    break;
            }
            //SellOption(UNLManager.OptionsManager.OptionDataDic.Values.ToList()[0], 1, 1);
            return result;
        }
        public AccountSummaryData AccountSummaryData { get; set; }
        public override void DoWorkAfterConnection()
        {
            //_doSellOneOrderTesting = false;
        }
        public Dictionary<string, OrderStatusData> OrderStatusDataDic { get; }

        public Dictionary<string, OptionNegotiator> OptionNegotiatorDic { get; set; }
        public OrderData SellOption(OptionData optionData, int quantity)
        {
            return SendOrder(optionData,quantity);
        }
        public OrderData BuyOption(OptionData optionData, int quantity)
        {
            return SendOrder(optionData, quantity, false);
        }
        private OrderData SendOrder(OptionData optionData, int quantity, bool sell = true)
        {
            OptionNegotiator optionNegotiator = new OptionNegotiator(APIWrapper, UNLManager, AccountSummaryData.SimulatorAccount);
            var orderData = optionNegotiator.TradeOption(optionData, sell, quantity);
            OptionNegotiatorDic[orderData.OrderId] = optionNegotiator;
            return orderData;
        }

        /// <summary>
        /// The method used by the OptionNegotiator when the order task accomplished.
        /// </summary>
        public void SendOrderTaskAccomplished(string orderId)
        {
            OptionNegotiatorDic.Remove(orderId);
            //TODO....
        }
        public void CancelOrder(string orderId)
        {
            OptionNegotiatorDic[orderId].CancelOrder(orderId);
        }
        public OrderData TestTrading(TradeOrderData tradeOrderData)
        {
            string optionKey = GetOptionKey(tradeOrderData.ExpiryDate, tradeOrderData.OptionType, tradeOrderData.Strike);
            OptionData optionData = UNLManager.OptionsManager.GetOptionData(optionKey);
            if(optionData == null)
                throw new Exception("The Option doesn't exist in the Option list!!!");

            OrderData orderData = tradeOrderData.OrderAction == OrderAction.SELL ? 
                SellOption(optionData, 1) : 
                BuyOption(optionData, 1);

            DefaultOrderType = tradeOrderData.OrderType;
            return orderData;

        }
        //public OrderData TestTrading(bool sell)
        //{
        //    string optionKey = GetOptionKey(new DateTime(2017, 08, 18), EOptionType.Put, 152.5);
        //    //string optionKey = GetOptionKey(new DateTime(2017, 08, 18), EOptionType.Call, 150);
        //    //string optionKey = GetOptionKey(new DateTime(2016, 3, 04), EOptionType.Call, 119);
        //    //DefaultOrderType = OrderType.MKT;
        //    OrderData orderData = sell ?
        //        SellOption(UNLManager.OptionsManager.GetOptionData(optionKey), 1) :
        //        BuyOption(UNLManager.OptionsManager.GetOptionData(optionKey), 1);
        //    DefaultOrderType = OrderType.LMT;
        //    return orderData;

        //}
        public string GetOptionKey(DateTime expiry, EOptionType optionType, double strike)
        {
            return $"{expiry}.{optionType}.{strike}";
        }
    }
}
