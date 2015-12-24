﻿using System.Collections.Generic;

namespace TNS.API.ApiDataObjects
{
    public interface ITradingApi
    {
        void ConnectToBroker();
        void RequestAccountData();
        void RequestContinousContractData(List<ContractBase> contracts);
        void RequestContinousPositionsData();
        string CreateOrder(OrderData order);
        void UpdateOrder(string orderId, OrderData order);
        void CancelOrder(string orderId);
    }
}