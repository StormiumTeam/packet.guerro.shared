﻿using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;

namespace package.stormiumteam.shared
{
	public class ReferencableGameObject : MonoBehaviour
	{
		private static readonly FastDictionary<int, ReferencableGameObject> s_GameObjects =
			new FastDictionary<int, ReferencableGameObject>();

		private List<Component> m_Components = new List<Component>();
		private GameObject      m_FastPathGameObject;

		private bool m_WasCreated;

		public ReadOnlyCollection<Component> Components => new ReadOnlyCollection<Component>(m_Components);

		private void OnCreate()
		{
			if (m_WasCreated)
				return;
			m_WasCreated         = true;
			m_FastPathGameObject = gameObject;

			m_Components = new List<Component>(GetComponents<Component>());

			RegisterReferencable();
		}

		private void Awake()
		{
			OnCreate();
		}

		private void OnEnable()
		{
			OnCreate();
		}

		public void Refresh()
		{
			m_Components = new List<Component>(GetComponents<Component>());
		}

		public T AddComponent<T>()
			where T : Component
		{
			var component = m_FastPathGameObject.AddComponent<T>();

			m_Components.Add(component);

			return component;
		}

		public T GetOrAddComponent<T>()
			where T : Component
		{
			var result = GetComponentFast<T>();
			return result.HasValue ? result.Value : AddComponent<T>();
		}

		public Result<T> GetComponentFast<T>()
			where T : Component
		{
			var length = m_Components.Count;
			for (var i = 0; i != length; i++)
			{
				if (m_Components[i] == null)
					continue;

				if (m_Components[i].GetType().IsSubclassOf(typeof(T))) return new Result<T>((T) m_Components[i], true);
			}

			var comp = GetComponent<T>();
			if (comp != null)
			{
				Refresh();
				return new Result<T>(comp, true);
			}

			return new Result<T>(null, false);
		}

		private void RegisterReferencable()
		{
			s_GameObjects[m_FastPathGameObject.GetInstanceID()] = this;
		}

		public static T GetComponent<T>(GameObject gameObject, bool createReferencable = true)
			where T : Component
		{
			ReferencableGameObject referencableGameObject;
			if (!Application.isPlaying)
			{
				referencableGameObject = gameObject.GetComponent<ReferencableGameObject>();
				if (referencableGameObject == null && createReferencable)
					gameObject.AddComponent<ReferencableGameObject>();
				return referencableGameObject == null
					? gameObject.GetComponent<T>()
					: referencableGameObject.GetComponentFast<T>().Value;
			}

			s_GameObjects.FastTryGet(gameObject.GetInstanceID(), out referencableGameObject);
			if (referencableGameObject == null)
			{
				referencableGameObject = gameObject.GetComponent<ReferencableGameObject>();
				if (referencableGameObject == null)
				{
					if (!createReferencable) return gameObject.GetComponent<T>();

					referencableGameObject = gameObject.AddComponent<ReferencableGameObject>();
					referencableGameObject.OnCreate();
				}
			}

			return referencableGameObject.GetComponentFast<T>().Value;
		}

		public static T GetComponent<T>(int referenceId, bool createReferencable = true)
			where T : Component
		{
			ReferencableGameObject referencableGameObject;
			s_GameObjects.FastTryGet(referenceId, out referencableGameObject);
			if (referencableGameObject == null)
			{
				Debug.LogError("Not found");
				return null;
			}

			return referencableGameObject.GetComponentFast<T>().Value;
		}

		public static GameObject FromId(int gameObjectId)
		{
			ReferencableGameObject referencableGameObject;
			s_GameObjects.FastTryGet(gameObjectId, out referencableGameObject);
			return referencableGameObject.m_FastPathGameObject;
		}

		public struct Result<T>
		{
			public bool HasValue;
			public T    Value;

			public Result(T value, bool hasValue)
			{
				Value    = value;
				HasValue = hasValue;
			}

			public static implicit operator T(Result<T> r)
			{
				return r.Value;
			}
		}
	}
}