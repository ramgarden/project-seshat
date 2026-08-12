using ProjectSeshat.App;
using Xunit;

namespace ProjectSeshat.Tests.App;

public sealed class OverlayPositionStoreTests
{
    [Fact]
    public void RoundTripsSavedPosition()
    {
        var dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "seshat-overlay-tests-" + System.Guid.NewGuid().ToString("N"));
        try
        {
            var store = new OverlayPositionStore(dir);
            Assert.Null(store.Load());

            store.Save(123, 456);

            var loaded = store.Load();
            Assert.NotNull(loaded);
            Assert.Equal(123, loaded.Value.X);
            Assert.Equal(456, loaded.Value.Y);

            // Reload from a fresh instance to confirm it was persisted to disk.
            var fresh = new OverlayPositionStore(dir);
            Assert.Equal((123, 456), fresh.Load());
        }
        finally
        {
            if (System.IO.Directory.Exists(dir))
            {
                System.IO.Directory.Delete(dir, recursive: true);
            }
        }
    }

    [Fact]
    public void Load_MissingFile_ReturnsNull()
    {
        var dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "seshat-overlay-missing-" + System.Guid.NewGuid().ToString("N"));
        try
        {
            var store = new OverlayPositionStore(dir);
            Assert.Null(store.Load());
        }
        finally
        {
            if (System.IO.Directory.Exists(dir))
            {
                System.IO.Directory.Delete(dir, recursive: true);
            }
        }
    }
}