﻿using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;

namespace com.noctuagames.sdk
{
    public class DefaultNativePlugin : INativePlugin
    {
        public void Init(List<string> activeBundleIds)
        {
        }

        public void OnApplicationPause(bool pause)
        {
        }

        public void ShowDatePicker(int year, int month, int day, int id)
        {
            throw new NotImplementedException();
        }

        public void TrackAdRevenue(string source, double revenue, string currency, Dictionary<string, IConvertible> extraPayload = null)
        {
        }

        public void TrackPurchase(string orderId, double amount, string currency, Dictionary<string, IConvertible> extraPayload = null)
        {
        }

        public void TrackCustomEvent(string name, Dictionary<string, IConvertible> extraPayload = null)
        {
        }

        public void PurchaseItem(string productId, Action<bool, string> callback)
        {
            throw new NotImplementedException();
        }

        public void GetActiveCurrency(string productId, Action<bool, string> callback)
        {
            throw new NotImplementedException();
        }

        public NativeAccount GetAccount(long userId, long gameId)
        {
            var rawAccounts = PlayerPrefs.GetString("NoctuaAccountContainer");
            var accounts = JsonConvert.DeserializeObject<List<NativeAccount>>(rawAccounts);
            
            return accounts.Find(a => a.PlayerId == userId && a.GameId == gameId);
        }

        public List<NativeAccount> GetAccounts()
        {
            var rawAccounts = PlayerPrefs.GetString("NoctuaAccountContainer");

            try
            {
                return JsonConvert.DeserializeObject<List<NativeAccount>>(rawAccounts) ?? new List<NativeAccount>();
            }
            catch (Exception e)
            {
                return new List<NativeAccount>();
            }
        }

        public void PutAccount(NativeAccount account)
        {
            var accounts = GetAccounts();
            
            accounts.RemoveAll(a => a.PlayerId == account.PlayerId && a.GameId == account.GameId);
            account.LastUpdated = DateTime.UtcNow;
            accounts.Add(account);
            
            PlayerPrefs.SetString("NoctuaAccountContainer", JsonConvert.SerializeObject(accounts));
        }

        public int DeleteAccount(NativeAccount account)
        {
            var accounts = GetAccounts();
            
            accounts.RemoveAll(a => a.PlayerId == account.PlayerId && a.GameId == account.GameId);

            PlayerPrefs.SetString("NoctuaAccountContainer", JsonConvert.SerializeObject(accounts));
            return 1;
        }
    }
}