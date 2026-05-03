using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MessageBroadcast.Core
{
    public static class DeviceIdentity
    {
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
