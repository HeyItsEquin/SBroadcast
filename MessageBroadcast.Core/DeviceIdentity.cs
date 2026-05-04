using System.IO;

namespace MessageBroadcast.Core
{
    public static class DeviceIdentity
    {
        // Either use existing UUID or create new one
        public static Guid LoadOrCreateUuid()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Paths.DeviceUuidPath)!);

            if (File.Exists(Paths.DeviceUuidPath))
                return Guid.Parse(File.ReadAllText(Paths.DeviceUuidPath));

            var id = Guid.NewGuid();
            File.WriteAllText(Paths.DeviceUuidPath, id.ToString());
            return id;
        }
    }
}
