﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using Unity.Entities;
using UnityEngine;

namespace package.stormiumteam.shared.modding
{
	// -------- -------- -------- -------- -------- -------- -------- -------- -------- /.
	// Public Group
	// -------- -------- -------- -------- -------- -------- -------- -------- -------- /.
	public partial class CModManager : ComponentSystem
	{
		public static event Action           OnAllPackageLoaded;
		public static event Action<CModInfo> OnNewMod;

		// -------- -------- -------- -------- -------- -------- -------- -------- -------- /.
		// Methods
		// -------- -------- -------- -------- -------- -------- -------- -------- -------- /.
        /// <summary>
        ///     Get the Mod of the target assembly
        /// </summary>
        /// <param name="assembly">The target assembly</param>
        /// <returns>The mod or null if nothing is found</returns>
        public CModInfo GetAssemblyMod(Assembly assembly)
		{
			return m_LoadedModsLookup[assembly];
		}

        /// <summary>
        ///     Get the world of the target mod
        /// </summary>
        /// <param name="modInfo">The target mod</param>
        /// <returns>The world of the mod</returns>
        public ModWorld GetModWorld(CModInfo modInfo)
		{
			return ModWorld.GetOrCreate(modInfo);
		}

		// -------- -------- -------- -------- -------- -------- -------- -------- -------- /.
		// Base methods
		// -------- -------- -------- -------- -------- -------- -------- -------- -------- /.
		protected override void OnUpdate()
		{
		}
	}

	// -------- -------- -------- -------- -------- -------- -------- -------- -------- /.
	// Private and static fields group
	// -------- -------- -------- -------- -------- -------- -------- -------- -------- /.
	/*
	 * We keep them static because we don't want to loose the values
	 * per world.
	 */
	public partial class CModManager
	{
		private static readonly List<CModInfo> m_LoadedMods = new List<CModInfo>();

		private static readonly Dictionary<Assembly, CModInfo> m_LoadedModsLookup
			= new Dictionary<Assembly, CModInfo>();

        /// <summary>
        ///     Get all the running mods (does not allocate)
        /// </summary>
        public static ReadOnlyCollection<CModInfo> LoadedMods => new ReadOnlyCollection<CModInfo>(m_LoadedMods);
	}

	// -------- -------- -------- -------- -------- -------- -------- -------- -------- /.
	// Internal group
	// -------- -------- -------- -------- -------- -------- -------- -------- -------- /.
	public partial class CModManager
	{
        /// <summary>
        ///     When this variable is at 1, we can't register internal packets anymore
        /// </summary>
        private static int s_RegisterCount;

		private static InternalRegistration s_CurrentInternalRegistration;

		private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
		{
			try
			{
				return assembly.GetTypes();
			}
			catch (ReflectionTypeLoadException e)
			{
				Debug.LogException(e);
				Debug.Log("Inners exceptions:");
				foreach (var inner in e.LoaderExceptions) Debug.Log(inner.GetType().Name + "\t" + inner.Message);
				Debug.Log("...................");
				return e.Types.Where(t => t != null);
			}
		}

		internal static void RegisterModInternal(Assembly[] assemblies, SModInfoData data)
		{
			var modInfo = new CModInfo(data, m_LoadedMods.Count);

			m_LoadedMods.Add(modInfo);
			foreach (var assembly in assemblies) m_LoadedModsLookup[assembly] = modInfo;

			// Load if it's not an integrated packet...
			if (data.Integration != IntegrationType.Integrated
			    && data.Integration != IntegrationType.InternalAndIntegrated
			    && data.Integration != IntegrationType.ExternalAndIntegrated)
			{
				// todo
			}

			// Call the bootstrappers...
			foreach (var assembly in assemblies)
			{
				var bootStrapperTypes = GetLoadableTypes(assembly).Where(t =>
					t.IsSubclassOf(typeof(CModBootstrap)) &&
					!t.IsAbstract &&
					!t.ContainsGenericParameters &&
					t.GetCustomAttributes(typeof(DisableAutoCreationAttribute), true).Length == 0);

				foreach (var bootstrapperType in bootStrapperTypes)
				{
					var bootstrap = Activator.CreateInstance(bootstrapperType) as CModBootstrap;
					bootstrap.SetModInfoInternal(modInfo);
					bootstrap.RegisterInternal();
				}
			}

			// Create the world
			var modWorld = ModWorld.GetOrCreate(modInfo);
			Debug.Assert(modWorld != null, "modWorld == null");

			// Create the systems
			foreach (var assembly in assemblies)
			{
				var systemTypes = GetLoadableTypes(assembly).Where(t =>
					t.IsSubclassOf(typeof(ComponentSystemBase)) &&
					!t.IsAbstract &&
					!t.ContainsGenericParameters &&
					t.GetCustomAttributes(typeof(DisableAutoCreationAttribute), true).Length == 0);

				foreach (var systemType in systemTypes)
					if (systemType.IsSubclassOf(typeof(ModComponentSystem)))
					{
						modWorld.GetExistingSystem(systemType);
					}
					// TODO: Verify for ClientComponentSystem
					else
					{
						var manager = World.DefaultGameObjectInjectionWorld.GetOrCreateSystem(systemType);

						var allFields = modWorld.GetType().GetFields();
						foreach (var field in allFields)
						{
							var shouldBeInjected = field
							                       .CustomAttributes
							                       .Count(a => a.GetType() == typeof(CModInfo.InjectAttribute)) > 0;
							if (shouldBeInjected) field.SetValue(manager, modInfo);
						}
					}
			}

			ScriptBehaviourUpdateOrder.UpdatePlayerLoop(World.DefaultGameObjectInjectionWorld);

			OnNewMod?.Invoke(modInfo);
		}

		public static void RegisterAssemblies(Assembly[]      assemblies, string displayName, string nameId,
		                                      IntegrationType integrationType)
		{
			RegisterModInternal(assemblies, new SModInfoData
			{
				DisplayName = displayName,
				NameId      = nameId,
				Type        = ModType.Package,
				Integration = integrationType
			});
		}

		public static void Invoke_LoadedAllPackages()
		{
			OnAllPackageLoaded?.Invoke();
		}

        /// <summary>
        ///     Begin the system to register new internal packets.
        /// </summary>
        /// <returns></returns>
        /// <exception cref="Exception">You cannot register new internal packets</exception>
        public static InternalRegistration BeginInternalRegistration()
		{
			if (s_RegisterCount != 0 || s_CurrentInternalRegistration != null) throw new Exception("You now cannot register new internal packets.");

			s_RegisterCount++;

			return s_CurrentInternalRegistration = new InternalRegistration();
		}

		public class InternalRegistration
		{
			internal InternalRegistration()
			{
				IsRunning = false;
			}

			public bool IsRunning { get; private set; }

			public void End()
			{
				IsRunning = false;
			}

			public void AddInternalPacket(string displayName, string nameId, Assembly[] assemblies)
			{
				RegisterModInternal(assemblies, new SModInfoData
				{
					DisplayName = displayName,
					NameId      = nameId,
					Type        = ModType.Package,
					Integration = IntegrationType.InternalAndIntegrated
				});
			}
		}
	}
}