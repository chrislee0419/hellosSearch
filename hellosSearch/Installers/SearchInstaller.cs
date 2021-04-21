using Zenject;
using HUISearch.Search;

namespace HUISearch.Installers
{
    public class SearchInstaller : Installer
    {
        public override void InstallBindings()
        {
            Container.BindInterfacesAndSelfTo<SearchManager>().AsSingle();

            Container.Bind<WordSearchEngine>().AsSingle();
            Container.BindInterfacesAndSelfTo<WordPredictionEngine>().AsSingle();
            Container.BindInterfacesAndSelfTo<LaserPointerManager>().AsSingle();
        }
    }
}
