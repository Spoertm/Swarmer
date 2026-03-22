using Blazored.LocalStorage;

namespace Swarmer.Web.Client.Services;

public sealed class DarkModeManager(ILocalStorageService localStorage)
{
	public event DarkModeToggleHandler? DarkModeToggle;
	public delegate void DarkModeToggleHandler();
	public bool DarkMode { get; private set; }

	public async Task Init()
	{
		DarkMode = await localStorage.GetItemAsync<bool>("darkmode");
		DarkModeToggle?.Invoke();
	}

	public async Task ToggleDarkMode()
	{
		DarkMode = !DarkMode;
		await localStorage.SetItemAsync("darkmode", DarkMode);
		DarkModeToggle?.Invoke();
	}
}
