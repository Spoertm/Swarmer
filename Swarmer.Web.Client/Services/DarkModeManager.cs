using Blazored.LocalStorage;

namespace Swarmer.Web.Client.Services;

public class DarkModeManager
{
	private readonly IServiceScopeFactory _scopeFactory;

	public bool DarkMode { get; private set; }

	public DarkModeManager(IServiceScopeFactory scopeFactory)
	{
		_scopeFactory = scopeFactory;
	}

	public async Task Init()
	{
		using IServiceScope scope = _scopeFactory.CreateScope();
		DarkMode = await scope.ServiceProvider.GetRequiredService<ILocalStorageService>().GetItemAsync<bool>("darkmode");
	}

	public void ToggleDarkMode()
	{
		DarkMode = !DarkMode;
		using IServiceScope scope = _scopeFactory.CreateScope();
		scope.ServiceProvider.GetRequiredService<ILocalStorageService>().SetItemAsync("darkmode", DarkMode);
	}
}
