using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;



namespace Emby.HueControl.Configuration

{

    public class PluginConfiguration : MediaBrowser.Model.Plugins.BasePluginConfiguration

    {
        public PluginConfiguration()

        {
            bridge = new Bridge { };
            Hooks = new Hook[] { };
            Options = new HueControl[] { };
         }

        public class Bridge
        {
            public string BridgeID { get; set; }
            public string HueAppID { get; set; }
            public string HueIP { get; set; }
            public string API { get; set; }
        }

        public Bridge bridge { get; set; }

        public LightGroup[] LightGroupsBase { get; set;  } 

        public HueControl[] Options { get; set; }


        public class LightGroup {
                public string Number { get; set; }
                public string Name { get; set; }
                public int OnPlay_Dim { get; set; }
                public int OnPause_Dim { get; set; }
                public int OnStop_Dim { get; set; }
                public int TransTime { get; set; }
                public bool Enabled { get; set; }

            }
        
        public class HueControl
        {
            public bool Enabled { get; set; }
            public string embyDeviceID { get; set; }
            //public string sceneSaveID { get; set; }
            public LightGroup[] LightGroups { get; set; }
            public bool withMovies { get; set; }
            public bool withEpisodes { get; set; }

        }

        public Hook[] Hooks { get; set; }

        public class Hook

        {

            public string URL { get; set; }
            public bool onPlay { get; set; }
            public bool onPause { get; set; }
            public bool onStop { get; set; }
            public bool onResume { get; set; }
            public bool onItemAdded { get; set; }
            public bool withMovies { get; set; }
            public bool withEpisodes { get; set; }
            public bool withSongs { get; set; }

        }

    }

}
