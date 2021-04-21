using Zenject;
using HUISearch.UI.Screens;
using HUISearch.UI.Settings;

namespace HUISearch.Installers
{
    public class UIInstaller : Installer
    {
        public override void InstallBindings()
        {
            Container.BindInterfacesAndSelfTo<SearchScreenManager>().AsSingle();
            Container.BindInterfacesAndSelfTo<SearchKeyboardScreenManager>().AsSingle();

            Container.BindInterfacesAndSelfTo<SearchSettingsTab>().AsSingle();
        }
    }
}
