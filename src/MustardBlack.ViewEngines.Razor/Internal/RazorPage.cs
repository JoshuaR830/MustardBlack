﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Html;
using Serilog;

namespace MustardBlack.ViewEngines.Razor.Internal
{
	/// <summary>
	/// Represents properties and methods that are needed in order to render a view that uses Razor syntax.
	/// </summary>
	public abstract class RazorPage : RazorPageBase
	{
		static readonly ILogger log = Log.ForContext(MethodBase.GetCurrentMethod().DeclaringType);

		readonly HashSet<string> renderedSections = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		bool renderedBody;
		bool ignoreBody;
		HashSet<string> ignoredSections;

		/// <summary>
		/// In a Razor layout page, renders the portion of a content page that is not within a named section.
		/// </summary>
		/// <returns>The HTML content to render.</returns>
		protected virtual IHtmlContent RenderBody()
		{
			if (BodyContent == null)
			{
				var message = "Resources.FormatRazorPage_MethodCannotBeCalled(nameof(RenderBody), Path)";
				throw new InvalidOperationException(message);
			}

			renderedBody = true;
			return BodyContent;
		}

		/// <summary>
		/// In a Razor layout page, ignores rendering the portion of a content page that is not within a named section.
		/// </summary>
		public void IgnoreBody()
		{
			ignoreBody = true;
		}

		/// <summary>
		/// Creates a named content section in the page that can be invoked in a Layout page using
		/// <see cref="RenderSection(string)"/> or <see cref="RenderSectionAsync(string, bool)"/>.
		/// </summary>
		/// <param name="name">The name of the section to create.</param>
		/// <param name="section">The <see cref="RenderAsyncDelegate"/> to execute when rendering the section.</param>
		public override void DefineSection(string name, RenderAsyncDelegate section)
		{
			if (name == null)
				throw new ArgumentNullException(nameof(name));

			if (section == null)
				throw new ArgumentNullException(nameof(section));

			if (SectionWriters.ContainsKey(name))
				throw new InvalidOperationException("Resources.FormatSectionAlreadyDefined(name)");

			SectionWriters[name] = section;
		}

		/// <summary>
		/// Returns a value that indicates whether the specified section is defined in the content page.
		/// </summary>
		/// <param name="name">The section name to search for.</param>
		/// <returns><c>true</c> if the specified section is defined in the content page; otherwise, <c>false</c>.</returns>
		public bool IsSectionDefined(string name)
		{
			if (name == null)
				throw new ArgumentNullException(nameof(name));

			EnsureMethodCanBeInvoked(nameof(IsSectionDefined));
			return PreviousSectionWriters.ContainsKey(name);
		}
		
		/// <summary>
		/// In layout pages, renders the content of the section named <paramref name="name"/>.
		/// </summary>
		/// <param name="name">The section to render.</param>
		/// <param name="required">Indicates if this section must be rendered.</param>
		/// <returns>An empty <see cref="IHtmlContent"/>.</returns>
		/// <remarks>The method writes to the <see cref="RazorPageBase.Output"/> and the value returned is a token
		/// value that allows the Write (produced due to @RenderSection(..)) to succeed. However the
		/// value does not represent the rendered content.</remarks>
		public HtmlString RenderSection(string name, bool required = false)
		{
			if (name == null)
				throw new ArgumentNullException(nameof(name));

			EnsureMethodCanBeInvoked(nameof(RenderSection));

			var task = RenderSectionAsyncCore(name, required);
			return task.GetAwaiter().GetResult();
		}

		/// <summary>
		/// In layout pages, asynchronously renders the content of the section named <paramref name="name"/>.
		/// </summary>
		/// <param name="name">The section to render.</param>
		/// <returns>
		/// A <see cref="Task{HtmlString}"/> that on completion returns an empty <see cref="IHtmlContent"/>.
		/// </returns>
		/// <remarks>The method writes to the <see cref="RazorPageBase.Output"/> and the value returned is a token
		/// value that allows the Write (produced due to @RenderSection(..)) to succeed. However the
		/// value does not represent the rendered content.</remarks>
		public Task<HtmlString> RenderSectionAsync(string name)
		{
			if (name == null)
				throw new ArgumentNullException(nameof(name));

			return RenderSectionAsync(name, required: true);
		}

		/// <summary>
		/// In layout pages, asynchronously renders the content of the section named <paramref name="name"/>.
		/// </summary>
		/// <param name="name">The section to render.</param>
		/// <param name="required">Indicates the <paramref name="name"/> section must be registered
		/// (using <c>@section</c>) in the page.</param>
		/// <returns>
		/// A <see cref="Task{HtmlString}"/> that on completion returns an empty <see cref="IHtmlContent"/>.
		/// </returns>
		/// <remarks>The method writes to the <see cref="RazorPageBase.Output"/> and the value returned is a token
		/// value that allows the Write (produced due to @RenderSection(..)) to succeed. However the
		/// value does not represent the rendered content.</remarks>
		/// <exception cref="InvalidOperationException">if <paramref name="required"/> is <c>true</c> and the section
		/// was not registered using the <c>@section</c> in the Razor page.</exception>
		public Task<HtmlString> RenderSectionAsync(string name, bool required)
		{
			if (name == null)
				throw new ArgumentNullException(nameof(name));

			EnsureMethodCanBeInvoked(nameof(RenderSectionAsync));
			return RenderSectionAsyncCore(name, required);
		}

		private async Task<HtmlString> RenderSectionAsyncCore(string sectionName, bool required)
		{
			if (renderedSections.Contains(sectionName))
				log.Warning("Section {section} already rendered", sectionName);

			if (PreviousSectionWriters.TryGetValue(sectionName, out var renderDelegate))
			{
				renderedSections.Add(sectionName);

				await renderDelegate();

				// Return a token value that allows the Write call that wraps the RenderSection \ RenderSectionAsync
				// to succeed.
				return HtmlString.Empty;
			}

			if (required)
			{
				// If the section is not found, and it is not optional, throw an error.
				throw new InvalidOperationException("Resources.FormatSectionNotDefined(viewContext.ExecutingFilePath, sectionName, ViewContext.View.Path)");
			}

			// If the section is optional and not found, then don't do anything.
			return null;
		}

		/// <summary>
		/// In layout pages, ignores rendering the content of the section named <paramref name="sectionName"/>.
		/// </summary>
		/// <param name="sectionName">The section to ignore.</param>
		public void IgnoreSection(string sectionName)
		{
			if (sectionName == null)
				throw new ArgumentNullException(nameof(sectionName));

			if (!PreviousSectionWriters.ContainsKey(sectionName))
			{
				// If the section is not defined, throw an error.
				throw new InvalidOperationException("Resources.FormatSectionNotDefined(ViewContext.ExecutingFilePath,sectionName,ViewContext.View.Path)");
			}

			if (ignoredSections == null)
				ignoredSections = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

			ignoredSections.Add(sectionName);
		}

		/// <inheritdoc />
		public override void EnsureRenderedBodyOrSections()
		{
			// a) all sections defined for this page are rendered.
			// b) if no sections are defined, then the body is rendered if it's available.
			if (this.PreviousSectionWriters != null && this.PreviousSectionWriters.Count > 0)
			{
				var sectionsNotRendered = this.PreviousSectionWriters.Keys.Except(this.renderedSections, StringComparer.OrdinalIgnoreCase);

				string[] sectionsNotIgnored;
				if (this.ignoredSections != null)
					sectionsNotIgnored = sectionsNotRendered.Except(this.ignoredSections, StringComparer.OrdinalIgnoreCase).ToArray();
				else
					sectionsNotIgnored = sectionsNotRendered.ToArray();

				if (sectionsNotIgnored.Length > 0)
					log.Warning("Section wasn't rendered {sectionNames}", sectionsNotIgnored);
			}
			else if (this.BodyContent != null && !this.renderedBody && !this.ignoreBody)
			{
				// There are no sections defined, but RenderBody was NOT called.
				// If a body was defined and the body not ignored, then RenderBody should have been called.

				log.Warning("Body wasn't rendered, `" + nameof(RenderBody) + "` wasn't called");
			}
		}

		public override void BeginContext(int position, int length, bool isLiteral)
		{
			const string BeginContextEvent = "Microsoft.AspNetCore.Mvc.Razor.BeginInstrumentationContext";

//			if (DiagnosticSource?.IsEnabled(BeginContextEvent) == true)
//			{
//				DiagnosticSource.Write(
//					BeginContextEvent,
//					new
//					{
//						httpContext = Context,
//						path = Path,
//						position = position,
//						length = length,
//						isLiteral = isLiteral,
//					});
//			}
		}

		public override void EndContext()
		{
			const string EndContextEvent = "Microsoft.AspNetCore.Mvc.Razor.EndInstrumentationContext";

//			if (DiagnosticSource?.IsEnabled(EndContextEvent) == true)
//			{
//				DiagnosticSource.Write(
//					EndContextEvent,
//					new
//					{
//						httpContext = Context,
//						path = Path,
//					});
//			}
		}

		private void EnsureMethodCanBeInvoked(string methodName)
		{
			if (PreviousSectionWriters == null)
				throw new InvalidOperationException("Resources.FormatRazorPage_MethodCannotBeCalled(methodName, Path)");
		}
	}
}