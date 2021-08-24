using PrintControler.MControl.Views;
using Prism.Ioc;
using Prism.Modularity;
using Prism.Regions;

namespace PrintControler.MControl {
    public class MControlModule : IModule {
        public void OnInitialized(IContainerProvider containerProvider) {
			var regionMan = containerProvider.Resolve<IRegionManager>();
			regionMan.RegisterViewWithRegion(PrintControler.Controler.Adventurer3Property.tabRegionName, typeof(Control));
		}

		public void RegisterTypes(IContainerRegistry containerRegistry) {
        }
    }
}