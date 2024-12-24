using System.Collections;
using com.noctuagames.sdk;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Tests.Runtime
{
    public class AuthenticationTest
    {
        [UnityTest]
        public IEnumerator TestAccountChanged() => UniTask.ToCoroutine(async () =>
        {
            try
            {
                var numAccountChanged = 0;
                Noctua.Auth.OnAccountChanged += _ => numAccountChanged++;

                await Noctua.InitAsync();

                var userBundle = await Noctua.Auth.AuthenticateAsync();

                Assert.IsNotNull(userBundle);
                Assert.AreEqual(1, numAccountChanged);

                userBundle = await Noctua.Auth.LoginWithEmailAsync("weteso6757@digopm.com", "aaaaaa");
                
                Assert.IsNotNull(userBundle);
                Assert.AreEqual(2, numAccountChanged);

                userBundle = await Noctua.Auth.LoginAsGuest();
                
                Assert.IsNotNull(userBundle);
                Assert.AreEqual(3, numAccountChanged);
            }
            catch (System.Exception e)
            {
                Assert.Fail($"Failed to login as guest: {e.Message}");
            }
        });
    }
}
