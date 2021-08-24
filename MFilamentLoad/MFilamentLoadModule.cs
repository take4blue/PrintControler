using PrintControler.MFilamentLoad.Views;
using Prism.Ioc;
using Prism.Modularity;
using Prism.Regions;

namespace PrintControler.MFilamentLoad {
	public class MFilamentLoadModule : IModule {
        public void OnInitialized(IContainerProvider containerProvider) {
			var regionMan = containerProvider.Resolve<IRegionManager>();
			regionMan.RegisterViewWithRegion(PrintControler.Controler.Adventurer3Property.tabRegionName, typeof(FilamentLoad));
		}

		public void RegisterTypes(IContainerRegistry containerRegistry) {            
        }
    }
}