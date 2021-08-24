using PrintControler.MModifyFile.Views;
using Prism.Ioc;
using Prism.Modularity;
using Prism.Regions;

namespace PrintControler.MModifyFile {
	public class MModifyFileModule : IModule {
		public void OnInitialized(IContainerProvider containerProvider) {
			var regionMan = containerProvider.Resolve<IRegionManager>();
			regionMan.RegisterViewWithRegion(PrintControler.Controler.Adventurer3Property.tabRegionName, typeof(FileParameter));
		}

		public void RegisterTypes(IContainerRegistry containerRegistry) {
		}
	}
}