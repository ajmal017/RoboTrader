﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using log4net.Repository.Hierarchy;
using TNS.API.ApiDataObjects;
using TNS.DbDAL;
using Infra.Bus;
using Infra.Enum;
using log4net;
using TNS.API;

namespace TNS.BL
{
    public class UNLManager : SimpleBaseLogic
    {
        private static readonly ILog Logger = LogManager.GetLogger(typeof(UNLManager));
        public UNLManager(MainSecurity mainSecurity, ITradingApi apiWrapper)
        {
            MainSecurity = mainSecurity;
            APIWrapper = apiWrapper;
            Logger.InfoFormat("UNLManager({0}) was created!", mainSecurity.Symbol);
        }

        internal string AddScheduledTaskOnUnl(TimeSpan span, Action task, bool reOccuring = false)
        {
            return AddScheduledTask(span, task, reOccuring);
        }

        internal void RemoveScheduledTaskOnUnl(string uniqueIdentifier)
        {
            RemoveScheduledTask(uniqueIdentifier);
        }
        private List<UnlMemberBaseManager> _memberManagersList;
        private ITradingApi APIWrapper { get; }
        private MainSecurity MainSecurity { get; }
        protected override string ThreadName => MainSecurity.Symbol + "_UNLManager_Work";
        protected override void HandleMessage(IMessage message)
        {
            switch (message.APIDataType)
            {
                case EapiDataTypes.SecurityData:
                    SendToAllComponents(message);
                    break;
                case EapiDataTypes.OptionData:
                    OptionsManager.HandleMessage(message);
                    break;
                case EapiDataTypes.PositionData:
                    PositionsDataBuilder.HandleMessage(message);
                    break;
                case EapiDataTypes.OrderData:
                    OrdersManager.HandleMessage(message);
                    break;
                case EapiDataTypes.BrokerConnectionStatus:
                    var connectionStatusMessage = (BrokerConnectionStatusMessage)message;
                    ConnectionStatus = connectionStatusMessage.Status;
                    if (connectionStatusMessage.AfterConnectionToApiWrapper)
                        DoWorkAfterConnection();
                    SendToAllComponents( message);
                    break;
                case EapiDataTypes.ContractDetailsData:
                    TradingTimeManager.HandleMessage(message);
                    break;
            }
        }

        private void SendToAllComponents(IMessage message)
        {
            if(_memberManagersList == null)
                return;
            foreach (var manager in _memberManagersList)
            {
                manager.HandleMessage(message);
            }
        }

        private bool IsConnected => ConnectionStatus == ConnectionStatus.Connected;
        private ConnectionStatus ConnectionStatus { get; set; }
        protected override void DoWorkAfterConnection()
        {
            CreateManagers();
        }

        private void CreateManagers()
        {
            _memberManagersList = new List<UnlMemberBaseManager>();

            TradingTimeManager = new TradingTimeManager(APIWrapper, MainSecurity, this);
            _memberManagersList.Add(TradingTimeManager);

            OptionsManager = new OptionsManager(APIWrapper, MainSecurity, this);
            _memberManagersList.Add(OptionsManager);

            PositionsDataBuilder = new PositionsDataBuilder(APIWrapper, MainSecurity,this, OptionsManager.OptionDataDic);
            _memberManagersList.Add(PositionsDataBuilder);

            TradingManager = new TradingManager(APIWrapper, MainSecurity, this);
           _memberManagersList.Add(TradingManager);

            OrdersManager = new OrdersManager(APIWrapper, MainSecurity, this);
            _memberManagersList.Add(OrdersManager);
        }

        private OptionsManager OptionsManager { get; set; }
        private PositionsDataBuilder PositionsDataBuilder { get; set; }
        private TradingManager TradingManager { get; set; }
        private TradingTimeManager TradingTimeManager { get; set; }
        private OrdersManager OrdersManager { get; set; }

    }
}