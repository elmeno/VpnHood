﻿namespace VpnHood.Server
{
    public enum AccessStatusCode
    {
        Ok,
        Expired,
        TrafficOverflow,
        Error,
        NotRegistered,
        TokenInvalid,
        Unpaid
    }
}