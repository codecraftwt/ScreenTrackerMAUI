namespace ScreenTracker1.Services
{
    public class DeviceIdProvider
    {
        private const string DeviceIdKey = "device_id";
        private string? _cachedDeviceId;

        public string GetDeviceId()
        {
            if (_cachedDeviceId != null)
                return _cachedDeviceId;

            var existing = Preferences.Get(DeviceIdKey, string.Empty);
            if (!string.IsNullOrEmpty(existing))
            {
                _cachedDeviceId = existing;
                Console.WriteLine($"[DeviceId] Loaded existing deviceId: {existing}");
                return existing;
            }

            var newId = Guid.NewGuid().ToString("N");
            Preferences.Set(DeviceIdKey, newId);
            _cachedDeviceId = newId;
            Console.WriteLine($"[DeviceId] Generated new deviceId: {newId}");
            return newId;
        }
    }
}
