﻿using System;
using System.Collections;
using Cysharp.Threading.Tasks;
using UnityEngine.Device;

namespace com.noctuagames.sdk
{
    public class Game
    {
        public int Id;

        public string Name;
    }

    public class User
    {
        public string Id;

        public string Nickname;

        public DateTime? DateOfBirth;
    }

    public class Player
    {
        public string UserId;

        public int GameId;

        public User User;
    }

    public class LoginRequest
    {
        public int GameId;

        public string DeviceId;

        public string BundleId;
    }

    public class LoginResponse
    {
        public string AccessToken;

        public Player Player;
    }

    public class NoctuaAuthService
    {
        public bool IsAuthenticated => !string.IsNullOrEmpty(_accessToken);
        public Player Player { get; private set; }

        private readonly Config _config;

        private string _accessToken;

        internal NoctuaAuthService(Config config)
        {
            _config = config;
        }

        public async UniTask<Player> LoginAsGuest()
        {
            var request = new HttpRequest(HttpMethod.Post, $"{_config.BaseUrl}/guests")
                .WithHeader("X-CLIENT-ID", _config.ClientId)
                .WithJsonBody(
                    new LoginRequest
                    {
                        DeviceId = SystemInfo.deviceUniqueIdentifier,
                        BundleId = Application.identifier
                    }
                );

            var response = await request.Send<LoginResponse>();
            _accessToken = response.AccessToken;
            Player = response.Player;

            return response.Player;
        }

        public IEnumerator Authenticate(Action<Player> onSuccess = null, Action<Exception> onError = null)
        {
            return LoginAsGuest().ToCoroutine(onSuccess, onError);
        }

        internal class Config
        {
            public string BaseUrl;
            public string ClientId;
        }
    }
}