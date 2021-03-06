﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace Modding
{
    /// <summary>
    /// Handles loading of mods.
    /// </summary>
	internal static class ModLoader
	{
        /// <summary>
        /// Loads the mod by searching for assemblies in hollow_knight_Data\Managed\Mods\
        /// </summary>
		public static void LoadMods()
        {
            if (Loaded)
            {
                return;
            }

            Logger.Log("[API] - Trying to load mods");
            string path = string.Empty;
            if (SystemInfo.operatingSystem.Contains("Windows"))
                path = Application.dataPath + "\\Managed\\Mods";
            else if (SystemInfo.operatingSystem.Contains("Mac"))
                path = Application.dataPath + "/Resources/Data/Managed/Mods/";
            else
                Logger.LogWarn($"Operating system of {SystemInfo.operatingSystem} is not known.  Unable to load mods.");

		    if (string.IsNullOrEmpty(path))
		    {
		        Loaded = true;
		        return;
		    }

		    foreach (string text2 in Directory.GetFiles(path, "*.dll"))
			{
				Logger.LogDebug("[API] - Loading assembly: " + text2);
				try
				{
					foreach (Type type in Assembly.LoadFile(text2).GetExportedTypes())
					{
						if (IsSubclassOfRawGeneric(typeof(Mod<>), type))
						{
						    Logger.LogDebug("[API] - Trying to instantiate mod<T>: " + type);

                            IMod mod = Activator.CreateInstance(type) as IMod;
						    if (mod == null) continue;
						    LoadedMods.Add((Mod)mod);
						}
						else if (!type.IsGenericType && type.IsClass && type.IsSubclassOf(typeof(Mod)))
						{
							Logger.LogDebug("[API] - Trying to instantiate mod: " + type);
							Mod mod2 = type.GetConstructor(new Type[0])?.Invoke(new object[0]) as Mod;
						    if (mod2 == null) continue;
						    LoadedMods.Add(mod2);
						}
					}
				}
				catch (Exception ex)
				{
				    Logger.LogError("[API] - Error: " + ex);
		            _errors.Add(string.Concat(text2, ": FAILED TO LOAD! Check ModLog.txt."));
				}
			}

		    foreach (Mod mod in LoadedMods.OrderBy(x => x.LoadPriority()))
		    {
		        try
		        {
                    LoadMod(mod, false);
		        }
		        catch (Exception ex)
		        {
		            _errors.Add(string.Concat(mod.GetType().Name, ": FAILED TO LOAD! Check ModLog.txt."));
		            Logger.LogError("[API] - Error: " + ex);
                }
            }


			GameObject gameObject = new GameObject();
            _draw = gameObject.AddComponent<ModVersionDraw>();
            UnityEngine.Object.DontDestroyOnLoad(gameObject);
			UpdateModText();
			Loaded = true;
            
		    ModHooks.Instance.SaveGlobalSettings();
		}

        private static List<string> _errors = new List<string>();

		static ModLoader()
		{
			Loaded = false;
			Debug = true;
		}

	    private static ModVersionDraw _draw;

	    private static void UpdateModText()
	    {
            StringBuilder builder = new StringBuilder();
	        builder.AppendLine("Modding API: " + ModHooks.Instance.ModVersion + (ModHooks.Instance.IsCurrent ? "" : " - New Version Available!") );
	        foreach (string error in _errors)
	        {
	            builder.AppendLine(error);
	        }

            foreach (Mod mod in LoadedMods)
	        {
	            if (ModHooks.Instance.GlobalSettings.ModEnabledSettings[mod.GetName()])
	            {
	                if (!_modVersionsCache.ContainsKey(mod.Name))
	                    _modVersionsCache.Add(mod.Name, $"{mod.GetVersion()} " + (mod.IsCurrent() ? string.Empty : " - New Version Available!"));

	                builder.AppendLine($"{mod.Name} : {_modVersionsCache[mod.Name]}");
                }
	        }
	        _draw.drawString = builder.ToString();

	    }

	    internal static void LoadMod(IMod mod)
	    {
	        ModHooks.Instance.GlobalSettings.ModEnabledSettings[mod.GetName()] = true;
            LoadMod(mod, false);
        }

        internal static void LoadMod(IMod mod, bool updateModText)
	    {
            

            mod.Initialize();


            if (!ModHooks.Instance.LoadedModsWithVersions.ContainsKey(mod.GetType().Name))
	            ModHooks.Instance.LoadedModsWithVersions.Add(mod.GetType().Name, mod.GetVersion());
            else
                ModHooks.Instance.LoadedModsWithVersions[mod.GetType().Name] = mod.GetVersion();

            if (ModHooks.Instance.LoadedMods.All(x => x != mod.GetType().Name))
                ModHooks.Instance.LoadedMods.Add(mod.GetType().Name);

            if (updateModText)
                UpdateModText();
        }

	    internal static void UnloadMod(ITogglableMod mod)
	    {
	        try
	        {
	            ModHooks.Instance.GlobalSettings.ModEnabledSettings[mod.GetName()] = false;
	            ModHooks.Instance.LoadedModsWithVersions.Remove(mod.GetType().Name);
	            ModHooks.Instance.LoadedMods.Remove(mod.GetType().Name);

                mod.Unload();
            }
	        catch (Exception ex)
	        {
	            Logger.LogError($"[API] - Failed to unload Mod - {mod.GetName()} - {Environment.NewLine} - {ex} ");
	        }

            UpdateModText();
        }

        /// <summary>
        /// Checks to see if a class is a subclass of a generic class.
        /// </summary>
        /// <param name="generic">Generic to compare against.</param>
        /// <param name="toCheck">Type to check</param>
        /// <returns></returns>
		private static bool IsSubclassOfRawGeneric(Type generic, Type toCheck)
		{
			while (toCheck != null && toCheck != typeof(object))
			{
				Type type = toCheck.IsGenericType ? toCheck.GetGenericTypeDefinition() : toCheck;
				if (generic == type)
				{
					return true;
				}
				toCheck = toCheck.BaseType;
			}
			return false;
		}
        /// <summary>
        /// Checks if the mod loads are done.
        /// </summary>
		public static bool Loaded;

        /// <summary>
        /// Is Debug Enabled
        /// </summary>
		public static bool Debug;

        /// <summary>
        /// List of loaded mods.
        /// </summary>
		public static List<Mod> LoadedMods = new List<Mod>();

        private static Dictionary<string, string> _modVersionsCache = new Dictionary<string, string>();
	    
    }
}
