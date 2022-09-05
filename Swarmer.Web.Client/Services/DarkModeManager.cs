﻿using Blazored.LocalStorage;

namespace Swarmer.Web.Client.Services;

public class DarkModeManager
{
	private readonly IServiceScopeFactory _scopeFactory;
	public event DarkModeToggleHandler? DarkModeToggle;
	public delegate void DarkModeToggleHandler();

	public bool DarkMode { get; private set; }

	public DarkModeManager(IServiceScopeFactory scopeFactory)
	{
		_scopeFactory = scopeFactory;
	}

	public async Task Init()
	{
		using IServiceScope scope = _scopeFactory.CreateScope();
		DarkMode = await scope.ServiceProvider.GetRequiredService<ILocalStorageService>().GetItemAsync<bool>("darkmode");
		DarkModeToggle?.Invoke();
	}

	public void ToggleDarkMode()
	{
		DarkMode = !DarkMode;
		DarkModeToggle?.Invoke();
		Console.WriteLine("Invoked!");
		using IServiceScope scope = _scopeFactory.CreateScope();
		scope.ServiceProvider.GetRequiredService<ILocalStorageService>().SetItemAsync("darkmode", DarkMode);
	}
}
