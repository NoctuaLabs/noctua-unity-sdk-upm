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
        public IEnumerator TestGuestLogin() => UniTask.ToCoroutine(async () =>
        {
            try
            {
                var loginResponse = await Noctua.Auth.LoginAsGuest();
                Assert.IsNotNull(loginResponse);
            }
            catch (HttpError e)
            {
                Assert.Fail($"Failed to login as guest: {e.StatusCode}");
            }
            
            catch (System.Exception e)
            {
                Assert.Fail($"Failed to login as guest: {e.Message}");
            }
        });
    }
}
