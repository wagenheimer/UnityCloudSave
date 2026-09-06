using System;
using NUnit.Framework;
using Wagenheimer.CloudSave;

namespace Wagenheimer.CloudSave.Editor.Setup.Tests
{
    public class CloudSaveControllerTests
    {
        static CloudSaveOptions Valid() => new()
        {
            SaveKey = "k",
            Serialize = () => new byte[] { 1 },
            Deserialize = _ => { },
        };

        [Test]
        public void Create_null_options_throws()
            => Assert.Throws<ArgumentNullException>(() => CloudSaveController.Create(null));

        [Test]
        public void Create_without_SaveKey_throws()
        {
            var o = Valid();
            o.SaveKey = null;
            Assert.Throws<ArgumentException>(() => CloudSaveController.Create(o));
        }

        [Test]
        public void Create_without_Serialize_throws()
        {
            var o = Valid();
            o.Serialize = null;
            Assert.Throws<ArgumentException>(() => CloudSaveController.Create(o));
        }

        [Test]
        public void Create_without_Deserialize_throws()
        {
            var o = Valid();
            o.Deserialize = null;
            Assert.Throws<ArgumentException>(() => CloudSaveController.Create(o));
        }

        [Test]
        public void Create_with_valid_options_returns_an_unstarted_controller()
        {
            var c = CloudSaveController.Create(Valid());
            Assert.IsNotNull(c);
            Assert.IsFalse(c.IsStarted);
        }

        [Test]
        public void ResetProgress_without_OnClearLocalSave_throws_until_configured()
        {
            var c = CloudSaveController.Create(Valid());
            Assert.Throws<InvalidOperationException>(() => c.ResetProgressAsync());
        }
    }
}
