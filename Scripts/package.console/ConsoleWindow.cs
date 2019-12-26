﻿using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;
using UnityEngine;

namespace package.stormiumteam.console
{
	/// <summary>
	///     Creates a console window that actually works in Unity
	///     You should add a script that redirects output using Console.Write to write to it.
	/// </summary>
	public class ConsoleWindow
	{
		private const int        STD_OUTPUT_HANDLE = -11;
		private       TextWriter oldOutput;

		public void Initialize()
		{
			//
			// Attach to any existing consoles we have
			// failing that, create a new one.
			//
			if (!AttachConsole(0x0ffffffff)) AllocConsole();

			oldOutput = Console.Out;

			try
			{
				var stdHandle      = GetStdHandle(STD_OUTPUT_HANDLE);
				var safeFileHandle = new SafeFileHandle(stdHandle, true);
				var fileStream     = new FileStream(safeFileHandle, FileAccess.Write);
				var encoding       = Encoding.ASCII;
				var standardOutput = new StreamWriter(fileStream, encoding);
				standardOutput.AutoFlush = true;
				Console.SetOut(standardOutput);
			}
			catch (Exception e)
			{
				Debug.Log("Couldn't redirect output: " + e.Message);
			}
		}

		public void Shutdown()
		{
			Console.SetOut(oldOutput);
			FreeConsole();
		}

		public void SetTitle(string strName)
		{
			SetConsoleTitle(strName);
		}

		[DllImport("kernel32.dll", SetLastError = true)]
		private static extern bool AttachConsole(uint dwProcessId);

		[DllImport("kernel32.dll", SetLastError = true)]
		private static extern bool AllocConsole();

		[DllImport("kernel32.dll", SetLastError = true)]
		private static extern bool FreeConsole();

		[DllImport("kernel32.dll", EntryPoint = "GetStdHandle", SetLastError = true, CharSet = CharSet.Auto, CallingConvention = CallingConvention.StdCall)]
		private static extern IntPtr GetStdHandle(int nStdHandle);

		[DllImport("kernel32.dll")]
		private static extern bool SetConsoleTitle(string lpConsoleTitle);
	}
}