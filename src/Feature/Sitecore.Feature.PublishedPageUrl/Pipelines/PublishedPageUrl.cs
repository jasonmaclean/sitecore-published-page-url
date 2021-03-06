﻿using Sitecore.Configuration;
using Sitecore.Data.Items;
using Sitecore.Diagnostics;
using Sitecore.Feature.PublishedPageUrl.Models;
using Sitecore.Links;
using Sitecore.Shell;
using Sitecore.Shell.Applications.ContentEditor.Pipelines.RenderContentEditor;
using Sitecore.Sites;
using Sitecore.Text;
using Sitecore.Web.UI.HtmlControls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;

namespace Sitecore.Feature.PublishedPageUrl.Pipelines
{
    public class ShowPublishedPageUrl
    {
        static List<RootUrl> RootUrls = new List<RootUrl>();

        public void LoadRootUrls(XmlNode node)
        {
            if (node == null
                || node.Attributes == null
                || node.Attributes["sitename"] == null
                || string.IsNullOrWhiteSpace(node.Attributes["sitename"].Value.ToString())
                || node.Attributes["language"] == null
                || string.IsNullOrWhiteSpace(node.Attributes["language"].Value.ToString())
                || node.Attributes["url"].Value == null
                || string.IsNullOrWhiteSpace(node.Attributes["url"].Value.ToString()))
            {
                Log.Error($"Sitecore.Feature.PublishedPageUrl.Pipelines.ShowPublishedPageUrl.LoadRootUrls -> Missing data in node.", this);
                return;
            }

            Log.Info($"Sitecore.Feature.PublishedPageUrl.Pipelines.ShowPublishedPageUrl.LoadRootUrls -> Found RootUrl for site: {node.Attributes["sitename"].Value}, Language: {node.Attributes["language"].Value}, Url: {node.Attributes["url"].Value}", this);

            RootUrls.Add(new RootUrl()
            {
                SiteName = node.Attributes["sitename"].Value,
                Language = node.Attributes["language"].Value,
                Url = node.Attributes["url"].Value
            });
        }

        public void Process(RenderContentEditorArgs args)
        {
            if (args == null)
                return;

            bool showDataSection = ShowDataSection;
            bool renderPageUrlSection = !showDataSection || UserOptions.ContentEditor.RenderCollapsedSections;

            if (renderPageUrlSection)
            {
                Item editingItem = args?.Item;

                if (editingItem != null && ItemHasPresentationDetails(editingItem))
                {
                    SiteContext itemSite = GetSiteContext(editingItem);

                    if (itemSite == null)
                    {
                        Log.Info($"Sitecore.Feature.PublishedPageUrl.Pipelines.ShowPublishedPageUrl -> No site found for item {editingItem.ID} in the RootUrls area of Feature.PublishedPageUrl.config", this);
                        return;
                    }

                    RootUrl rootUrl = RootUrls.FirstOrDefault(x => x.SiteName == itemSite.Name && x.Language.ToLowerInvariant() == editingItem.Language.Name.ToLowerInvariant());

                    string url = string.Empty;
                    bool rootUrlMissing = true;

                    if (rootUrl != null)
                    {
                        url = rootUrl.Url.ToLowerInvariant();

                        if (!string.IsNullOrWhiteSpace(url) && url.EndsWith("/"))
                            url = url.Remove(url.Length - 1);

                        using (new SiteContextSwitcher(itemSite))
                        {
                            using (new Globalization.LanguageSwitcher(editingItem.Language))
                            {
                                var options = LinkManager.GetDefaultUrlOptions();
                                options.AlwaysIncludeServerUrl = false;
                                options.SiteResolving = true;

                                var path = LinkManager.GetItemUrl(editingItem, options).ToLowerInvariant();
                                path = path.Replace(url, "").Replace(":443", "").Replace(":80", "");

                                url += path;
                            }
                        }

                        rootUrlMissing = false;
                    }

                    StringBuilder sectionText = new StringBuilder();

                    sectionText.Append("<table class=\"scEditorQuickInfo\">");
                    sectionText.Append("<colgroup>");
                    sectionText.Append("<col style=\"white-space:nowrap\" align=\"right\" valign=\"top\" />");
                    sectionText.Append("<col style=\"white-space:nowrap\" valign=\"top\" />");
                    sectionText.Append("</colgroup>");
                    sectionText.Append("<tbody>");

                    if (rootUrlMissing)
                    {
                        sectionText.Append("<tr>");
                        sectionText.Append($"<td>{Settings.GetSetting("Sitecore.Feature.PublishedPageUrl.NoUrlFoundWarningLabel")}:&nbsp;</td>");
                        sectionText.Append($"<td>{Settings.GetSetting("Sitecore.Feature.PublishedPageUrl.NoUrlFoundWarning")}</td>");
                        sectionText.Append("</tr>");
                    }
                    else
                    {
                        sectionText.Append("<tr>");
                        sectionText.Append($"<td>{Settings.GetSetting("Sitecore.Feature.PublishedPageUrl.UrlLabel")}:&nbsp;</td>");
                        sectionText.Append("<td>");

                        if (Settings.GetBoolSetting("Sitecore.Feature.PublishedPageUrl.EnableLinkInUrl", true) && url.Contains("http"))
                        {
                            sectionText.Append($"<a href=\"{url}\" target=\"_blank\">{url}</a>");
                        }
                        else
                        {
                            sectionText.Append($"<input readonly=\"readonly\" onclick=\"javascript: this.select(); return false\" value=\"{url}\">");
                        }

                        sectionText.Append("</td>");
                        sectionText.Append("</tr>");
                    }

                    sectionText.Append("</tbody>");
                    sectionText.Append("</table>");

                    args.EditorFormatter.RenderSectionBegin(args.Parent, "PublishedPageUrlDataSection", "PublishedPageUrlDataSection", Settings.GetSetting("Sitecore.Feature.PublishedPageUrl.DataSectionTitle"), "People/32x32/atom.png", showDataSection, UserOptions.ContentEditor.RenderCollapsedSections);
                    args.EditorFormatter.AddLiteralControl(args.Parent, sectionText.ToString());
                    args.EditorFormatter.RenderSectionEnd(args.Parent, renderPageUrlSection, true);
                }
            }
        }

        private bool ItemHasPresentationDetails(Item item)
        {
            if (item?.Fields[FieldIDs.LayoutField] == null)
                return false;

            if (string.IsNullOrWhiteSpace(item.Fields[FieldIDs.LayoutField].Value))
                return false;

            return true;
        }

        private bool ShowDataSection
        {
            get
            {
                try
                {
                    UrlString collapsedSections = new UrlString(Registry.GetString("/Current_User/Content Editor/Sections/Collapsed"));
                    string preferenceValue = collapsedSections["PublishedPageUrlDataSection"];

                    if (string.IsNullOrWhiteSpace(preferenceValue) || (preferenceValue == "1"))
                        return true;
                }
                catch (Exception ex)
                {
                    Log.Error($"Sitecore.Feature.PublishedPageUrl.Pipelines.ShowPublishedPageUrl.ShowDataSection -> {ex}", this);
                }

                return false;
            }
        }

        private SiteContext GetSiteContext(Item item)
        {
            try
            {
                string siteName = item.Paths.FullPath.Split('/').Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().ToList()[2].ToLowerInvariant();
                var siteContext = Factory.GetSite(siteName);

                if (siteContext == null)
                {
                    Log.Info($"Sitecore.Feature.PublishedPageUrl.Pipelines.ShowPublishedPageUrl.GetSiteContext -> No site context found based off item path. Assuming \"website\".", this);
                    siteContext = Factory.GetSite("website");
                }

                return siteContext;
            }
            catch (Exception ex)
            {
                Log.Error($"Sitecore.Feature.PublishedPageUrl.Pipelines.ShowPublishedPageUrl.GetSiteContext -> {ex}", this);
            }
            return null;
        }
    }
}