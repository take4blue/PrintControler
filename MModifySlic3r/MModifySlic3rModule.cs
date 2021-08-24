using PrintControler.MModifySlic3r.Views;
using Prism.Ioc;
using Prism.Modularity;
using Prism.Regions;

namespace PrintControler.MModifySlic3r {
    public class MModifySlic3rModule : IModule
    {
        public void OnInitialized(IContainerProvider containerProvider) {
			var regionMan = containerProvider.Resolve<IRegionManager>();
			regionMan.RegisterViewWithRegion(PrintControler.Controler.Adventurer3Property.tabRegionName, typeof(Parameter));
		}

		public void RegisterTypes(IContainerRegistry containerRegistry) {
        }
    }
}