﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Infra;
using Infra.Enum;
using Infra.Extensions;
using TNS.API.ApiDataObjects;

namespace TNS.API
{
    /// <summary>
    /// The object contains all the parameters for determining the options 
    /// that will be received from the broker.<para></para>
    /// For each UNL will be a different object
    /// </summary>
    public class OptionToLoadParameters
    {

        public OptionToLoadParameters(BaseSecurityData baseSecurityData)
        {
            BaseSecurityData = baseSecurityData;
            _unlLastPrice = baseSecurityData.LastPrice;
            //SetStrikeThreshold();
            SetMinMaxStrike();
        }

        private double _unlLastPrice;

        private double _strikeThreshold;

        private void SetStrikeThreshold()
        {
            double highStrikeRatio = (double)AllConfigurations.AllConfigurationsObject.Session.HighStrikePercentage / 100;

            double factor = (UnlPrice / 2000);

            _strikeThreshold = highStrikeRatio *(1 -factor);

            //if (_unlLastPrice <= 200)
            //{
            //    _strikeThreshold = highStrikeRatio;
            //    return;
            //}
            //if (_unlLastPrice > 200 && (_unlLastPrice <= 500))
            //{
            //    _strikeThreshold = highStrikeRatio * .9;
            //    return;
            //}
            //if (_unlLastPrice > 500 && (_unlLastPrice <= 800))
            //{
            //    _strikeThreshold = highStrikeRatio * .7;
            //    return;
            //}
            //_strikeThreshold = highStrikeRatio * .5;

        }

        private void SetMinMaxStrike()
        {
           
            double highStrikeRatio = (double)AllConfigurations.AllConfigurationsObject.Session.HighStrikePercentage / 100;

            double factor = (UnlPrice / 2000);

            double strikeThreshold = highStrikeRatio * (1 - factor);

            MinStrike = UnlPrice * (1 - strikeThreshold);
            MaxStrike = UnlPrice * (1 + strikeThreshold);
            //var minStrike = (UnlPrice * (1 - (double)(
            //                                     AllConfigurations.AllConfigurationsObject.Session.HighStrikePercentage) / 100));
            //MinStrike = minStrike;

            //var maxStrike = (UnlPrice * (1 + (double)(
            //                                 AllConfigurations.AllConfigurationsObject.Session.HighStrikePercentage) / 100));

            //MaxStrike = maxStrike;
        }
        public BaseSecurityData BaseSecurityData { get; set; }

        public string Symbol => BaseSecurityData.GetContract().Symbol;

        //public double UnlPrice => BaseSecurityData.LastPrice;
        public double UnlPrice => BaseSecurityData.LastPrice <= 0 ? 150 : BaseSecurityData.LastPrice;

        public int MinDaysToExpiration => AllConfigurations.AllConfigurationsObject.Session.MinimumDaysToExpiration;
        public int MaxDaysToExpiration => AllConfigurations.AllConfigurationsObject.Session.MaxmumDaysToExpiration;

        //private double CallMinStrike
        //{
        //    get;
        //    set;
        //    //{
        //    //    var callMinStrike = (UnlPrice * (1 - (double)(
        //    //                      AllConfigurations.AllConfigurationsObject.Session.HighStrikePercentage) / 100));

        //    //    callMinStrike = Math.Max(_unlLastPrice * (1 - _strikeThreshold), callMinStrike);
        //    //    return callMinStrike;
        //    //}
        //}

        private double MaxStrike
        {
            get;
            set;
            //{
            //    var callMaxStrike =
            //    (UnlPrice * (1 + (double) (AllConfigurations.AllConfigurationsObject.Session.LowStrikePercentage) /
            //                 100));

            //    callMaxStrike = Math.Min(_unlLastPrice * (1 - _strikeThreshold), callMaxStrike);
            //    return callMaxStrike;
            //}
        }

        //private double MaxStrike
        //{
        //    get; set;
        //    //{
        //    //    var putMaxStrike =
        //    //        (UnlPrice * (1 + (double)(AllConfigurations.AllConfigurationsObject.Session.HighStrikePercentage) / 100));

        //    //    putMaxStrike = Math.Min(_unlLastPrice * (1 - _strikeThreshold), putMaxStrike);
        //    //    return putMaxStrike;
        //    //}
        //}

        private double MinStrike
        {
            get;
            set;
            //{
            //    var putMinStrike = (UnlPrice * (1 - (double)(AllConfigurations.AllConfigurationsObject.Session.LowStrikePercentage) / 100));

            //    putMinStrike = Math.Max(_unlLastPrice * (1 - _strikeThreshold), putMinStrike);
            //    return putMinStrike;
            //}
        }
        //public double CallMinStrike => (UnlPrice * (1 - (double)(AllConfigurations.AllConfigurationsObject.Session.HighStrikePercentage) / 100));
        //public double CallMaxStrike => (UnlPrice * (1 + (double)(AllConfigurations.AllConfigurationsObject.Session.LowStrikePercentage) / 100));
        //public double PutMaxStrike =>  (UnlPrice * (1 + (double)(AllConfigurations.AllConfigurationsObject.Session.HighStrikePercentage) / 100));
        //public double PutMinStrike =>  (UnlPrice * (1 - (double)(AllConfigurations.AllConfigurationsObject.Session.LowStrikePercentage) / 100));

        public int Multiplier => BaseSecurityData.Multiplier;
        /// <summary>
        /// Get or Set the number of request Data for options.
        /// </summary>
        public int RequestOptionMarketDataCount { get;private set; }

        public void IncreamentRequestOptionMarketDataCounter()
        {
            RequestOptionMarketDataCount++;
        }
     

        public OptionContract OptionContractPivotToLoad
        {
            get
            {
                ContractBase contractBase = BaseSecurityData.GetContract();
                OptionContract optionContract = new OptionContract
                {
                    Exchange = contractBase.Exchange,
                    Multiplier = Multiplier,
                    Symbol = contractBase.Symbol,
                    SecurityType = SecurityType.Option,
                    Strike = 0,
                    OptionType = EOptionType.None
                };
                return optionContract;
            }
        }
        


        /// <summary>
        /// Check if the option is between the striks boundary.
        /// 
        /// </summary>
        /// <param name="optionContract"></param>
        /// <returns></returns>
        public bool IsOptionWithinLoadBoundaries(OptionContract optionContract)
        {
            //Get only Monthly option chain, not weekly!
            if (optionContract.Expiry.Equals(optionContract.Expiry.GetThirdFridayOfMonth()) == false)
                return false;
            //Check expiration boundaries:
            if (DateTime.Now.AddDays(MinDaysToExpiration) > optionContract.Expiry)
                return false;
            if (optionContract.Expiry > DateTime.Now.AddDays(MaxDaysToExpiration))
                return false;
            //Check strike boundaries:
            if ((optionContract.Strike > MaxStrike) || (optionContract.Strike < MinStrike))
                return false;


            ////Check if the Delta within the allowed offset:
            ////if(optionContract.)
            ////Check strike boundaries:
            //switch (optionContract.OptionType)
            //{
            //    case EOptionType.Call:
            //        if ((optionContract.Strike > MaxStrike) || (optionContract.Strike < MinStrike))
            //            return false;
            //        break;
            //    case EOptionType.Put:
            //        if ((optionContract.Strike > MaxStrike) || (optionContract.Strike < MinStrike))
            //            return false;
            //        break;
            //    default:
            //        return false;
            //}
            //IncreamentRequestOptionMarketDataCounter();
            return true;
        }

        public override string ToString()
        {
            return $"Symbol: {Symbol}, MinDaysToExpiration: {MinDaysToExpiration}," +
                   $" MaxDaysToExpiration: {MaxDaysToExpiration}," +
                   $" MinStrikeToLoad: {MinStrike}, MaxStrikeToLoad: {MaxStrike}," +
                   $" Multiplier: {Multiplier}";
        }
    }
}
