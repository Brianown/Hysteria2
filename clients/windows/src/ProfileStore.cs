using System;
using System.Collections.Generic;
using System.IO;
using System.Web.Script.Serialization;

namespace HysteriaLite
{
    public sealed class ProfileStore
    {
        private readonly string path;

        public ProfileStore()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string directory = Path.Combine(appData, "HysteriaLite");
            Directory.CreateDirectory(directory);
            path = Path.Combine(directory, "profiles.json");
        }

        public List<HysteriaProfile> Load()
        {
            try
            {
                if (!File.Exists(path))
                    return new List<HysteriaProfile>();
                JavaScriptSerializer serializer = new JavaScriptSerializer();
                List<HysteriaProfile> list = serializer.Deserialize<List<HysteriaProfile>>(File.ReadAllText(path));
                return list ?? new List<HysteriaProfile>();
            }
            catch (Exception)
            {
                return new List<HysteriaProfile>();
            }
        }

        public void Save(List<HysteriaProfile> profiles)
        {
            try
            {
                JavaScriptSerializer serializer = new JavaScriptSerializer();
                File.WriteAllText(path, serializer.Serialize(profiles));
            }
            catch (Exception)
            {
            }
        }
    }
}
