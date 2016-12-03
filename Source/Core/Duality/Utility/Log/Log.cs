﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Duality
{
	/// <summary>
	/// Listens for log entries and writes them to registered <see cref="ILogOutput">ILogOutputs</see>.
	/// </summary>
	public sealed class Log
	{
		/// <summary>
		/// Holds a Logs state values.
		/// </summary>
		public class SharedState
		{
			private int indent = 0;

			/// <summary>
			/// [GET / SET] The Logs indent value.
			/// </summary>
			public int Indent
			{
				get { return this.indent; }
				internal set { this.indent = value; }
			}
		}


		private List<ILogOutput> strOut = null;
		private SharedState      state  = null;
		private string           name   = "Log";
		private string           prefix = "[Log] ";

		/// <summary>
		/// [GET] The Log's name
		/// </summary>
		public string Name
		{
			get { return this.name; }
		}
		/// <summary>
		/// [GET] The Log's prefix, which is automatically determined by its name.
		/// </summary>
		public string Prefix
		{
			get { return this.prefix; }
		}
		/// <summary>
		/// [GET] The Log's current indent level.
		/// </summary>
		public int Indent
		{
			get { return this.state.Indent; }
		}
		/// <summary>
		/// [GET] Enumerates all the output writers of this log.
		/// </summary>
		public IEnumerable<ILogOutput> Outputs
		{
			get { return this.strOut; }
		}

		/// <summary>
		/// Creates a new Log.
		/// </summary>
		/// <param name="name">The Logs name.</param>
		/// <param name="stateHolder">The Logs state value holder that may be shared with other Logs.</param>
		/// <param name="output">It will be initially connected to the specified outputs.</param>
		public Log(string name, SharedState stateHolder, params ILogOutput[] output)
		{
			this.state = stateHolder;
			this.name = name;
			this.prefix = "[" + name + "] ";
			this.strOut = new List<ILogOutput>(output);
		}
		/// <summary>
		/// Creates a new Log.
		/// </summary>
		/// <param name="name">The Logs name</param>
		/// <param name="output">It will be initially connected to the specified outputs.</param>
		public Log(string name, params ILogOutput[] output) : this(name, new SharedState(), output) {}

		/// <summary>
		/// Adds an output to write log entries to.
		/// </summary>
		/// <param name="writer"></param>
		public void AddOutput(ILogOutput writer)
		{
			this.strOut.Add(writer);
		}
		/// <summary>
		/// Removes a certain output.
		/// </summary>
		/// <param name="writer"></param>
		public void RemoveOutput(ILogOutput writer)
		{
			this.strOut.Remove(writer);
		}

		/// <summary>
		/// Increases the current log entry indent.
		/// </summary>
		public void PushIndent()
		{
			this.state.Indent++;
		}
		/// <summary>
		/// Decreases the current log entry indent.
		/// </summary>
		public void PopIndent()
		{
			this.state.Indent--;
		}

		private void Write(LogMessageType type, string msg, object context)
		{
			Profile.TimeLog.BeginMeasure();

			// If a null message is provided, log that. Don't throw an exception, since logging isn't expected to throw.
			if (msg == null) msg = "[null message]";

			// Check whether the message contains null characters. If it does, crop it, because it's probably broken.
			int nullCharIndex = msg.IndexOf('\0');
			if (nullCharIndex != -1)
			{
				msg = msg.Substring(0, Math.Min(nullCharIndex, 50)) + " | Contains '\0' and is likely broken.";
			}

			// Forward the message to all outputs
			LogEntry entry = new LogEntry(type, msg, this.state.Indent);
			foreach (ILogOutput log in this.strOut)
			{
				try
				{
					log.Write(entry, context, this);
				}
				catch (Exception)
				{
					// Don't allow log outputs to throw unhandled exceptions,
					// because they would result in another log - and more exceptions.
				}
			}
			Profile.TimeLog.EndMeasure();
		}
		private string FormatMessage(string format, object[] obj)
		{
			if (obj == null || obj.Length == 0) return format;
			string msg;
			try
			{
				msg = string.Format(System.Globalization.CultureInfo.InvariantCulture, format, obj);
			}
			catch (Exception e)
			{
				// Don't allow log message formatting to throw unhandled exceptions,
				// because they would result in another log - and probably more exceptions.

				// Instead, embed format, arguments and the exception in the resulting
				// log message, so the user can retrieve all necessary information for
				// fixing his log call.
				msg = format + Environment.NewLine;
				if (obj != null)
				{
					try
					{
						msg += obj.ToString(", ") + Environment.NewLine;
					}
					catch (Exception)
					{
						msg += "(Error in ToString call)" + Environment.NewLine;
					}
				}
				msg += LogFormat.Exception(e);
			}
			return msg;
		}
		private object FindContext(object[] obj)
		{
			if (obj == null || obj.Length == 0) return null;
			for (int i = 0; i < obj.Length; i++)
			{
				if (obj[i] is GameObject || obj[i] is Component || obj[i] is Resource || obj[i] is IContentRef)
					return obj[i];
			}
			return obj[0];
		}

		/// <summary>
		/// Writes a new log entry.
		/// </summary>
		/// <param name="format"></param>
		/// <param name="obj"></param>
		public void Write(string format, params object[] obj)
		{
			this.Write(LogMessageType.Message, this.FormatMessage(format, obj), this.FindContext(obj));
		}
		/// <summary>
		/// Writes a new warning log entry.
		/// </summary>
		/// <param name="format"></param>
		/// <param name="obj"></param>
		public void WriteWarning(string format, params object[] obj)
		{
			this.Write(LogMessageType.Warning, this.FormatMessage(format, obj), this.FindContext(obj));
		}
		/// <summary>
		/// Writes a new error log entry.
		/// </summary>
		/// <param name="format"></param>
		/// <param name="obj"></param>
		public void WriteError(string format, params object[] obj)
		{
			this.Write(LogMessageType.Error, this.FormatMessage(format, obj), this.FindContext(obj));
		}
	}
}