using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.UIElements;
using com.noctuagames.sdk.LiveOpsCampaign;

namespace com.noctuagames.sdk.Inspector
{
    /// <summary>
    /// "Campaigns" tab — read-only view of the server-driven campaign resolution:
    /// every campaign with shown / skipped + reason, plus the raw merged JSON.
    /// Sandbox-gated by the same flag as the rest of the Inspector; a production
    /// build spawns nothing here.
    /// </summary>
    public partial class NoctuaInspectorController
    {
        private bool _campaignRawExpanded;

        private void RenderCampaigns(ref int ok, ref int failing, ref int inflight)
        {
            var campaign = Noctua.LiveOpsCampaign;
            if (campaign?.Manager == null)
            {
                var muted = new Label("Campaign feature not initialised (no local or remote campaign config).");
                muted.style.color = TextMid;
                muted.style.fontSize = 13;
                muted.style.whiteSpace = WhiteSpace.Normal;
                muted.style.paddingLeft = 12;
                muted.style.paddingRight = 12;
                muted.style.paddingTop = 12;
                _listContainer.Add(muted);
                return;
            }

            var manager = campaign.Manager;
            var resolutions = SnapshotResolutions(manager);

            var box = new VisualElement();
            box.style.flexShrink = 0;
            box.style.paddingLeft = 12;
            box.style.paddingRight = 12;
            box.style.paddingTop = 12;
            box.style.paddingBottom = 4;

            var head = new Label($"Resolution — {resolutions.Count} campaign(s), schema v{manager.Config.SchemaVersion}");
            head.style.color = TextLo;
            head.style.fontSize = 12;
            head.style.paddingBottom = 6;
            box.Add(head);

            if (resolutions.Count == 0)
            {
                var muted = new Label("(no campaigns in config)");
                muted.style.color = TextMid;
                muted.style.fontSize = 13;
                box.Add(muted);
            }
            else
            {
                foreach (var r in resolutions)
                {
                    box.Add(BuildCampaignRow(r));
                    if (r.Eligible) ok++;
                    else failing++;
                }
            }

            _listContainer.Add(box);
            _listContainer.Add(BuildCampaignRawSection(manager.Config));
        }

        private List<CampaignResolution> SnapshotResolutions(CampaignManager manager)
        {
            // One unfiltered pass — every campaign, regardless of engagement type.
            manager.GetActiveCampaigns();
            return new List<CampaignResolution>(manager.LastResolutions);
        }

        private VisualElement BuildCampaignRow(CampaignResolution r)
        {
            var row = new VisualElement();
            row.style.flexShrink = 0;
            row.style.flexDirection = FlexDirection.Column;
            row.style.paddingTop = 8;
            row.style.paddingBottom = 8;
            row.style.borderBottomWidth = 1;
            row.style.borderBottomColor = Stroke;

            var top = new VisualElement();
            top.style.flexDirection = FlexDirection.Row;
            top.style.alignItems = Align.Center;

            var badge = new Label(r.Eligible ? "SHOWN" : "SKIP");
            badge.style.color = r.Eligible ? Ok : Warn;
            badge.style.fontSize = 12;
            badge.style.unityFontStyleAndWeight = FontStyle.Bold;
            badge.style.minWidth = 52;
            badge.style.flexShrink = 0;
            top.Add(badge);

            var id = new Label(string.IsNullOrEmpty(r.Id) ? "(no id)" : r.Id);
            id.style.color = TextHi;
            id.style.fontSize = 13;
            id.style.flexGrow = 1;
            top.Add(id);

            var engagement = new Label(r.EngagementType ?? "—");
            engagement.style.color = TextLo;
            engagement.style.fontSize = 11;
            top.Add(engagement);

            row.Add(top);

            var reason = new Label(r.Reason);
            reason.style.color = r.Eligible ? TextMid : Err;
            reason.style.fontSize = 12;
            reason.style.whiteSpace = WhiteSpace.Normal;
            reason.style.marginTop = 2;
            row.Add(reason);

            return row;
        }

        private VisualElement BuildCampaignRawSection(CampaignConfig config)
        {
            var box = new VisualElement();
            box.style.flexShrink = 0;
            box.style.paddingLeft = 12;
            box.style.paddingRight = 12;
            box.style.paddingTop = 12;
            box.style.paddingBottom = 12;

            string json;
            try { json = JsonConvert.SerializeObject(config, Formatting.Indented); }
            catch { json = "(serialization failed)"; }

            var headerRow = new VisualElement();
            headerRow.style.flexDirection = FlexDirection.Row;
            headerRow.style.alignItems = Align.Center;
            headerRow.style.paddingTop = 6;
            headerRow.style.paddingBottom = 6;
            headerRow.RegisterCallback<ClickEvent>(_ =>
            {
                _campaignRawExpanded = !_campaignRawExpanded;
                _dirty = true;
            });

            var head = new Label("merged campaign JSON");
            head.style.color = TextLo;
            head.style.fontSize = 12;
            head.style.flexGrow = 1;
            headerRow.Add(head);

            var chev = new Label(_campaignRawExpanded ? "▼" : "▶");
            chev.style.color = TextLo;
            chev.style.fontSize = 12;
            headerRow.Add(chev);
            box.Add(headerRow);

            if (_campaignRawExpanded)
            {
                var wrap = new VisualElement();
                wrap.style.backgroundColor = Bg2;
                wrap.style.paddingLeft = 12;
                wrap.style.paddingRight = 12;
                wrap.style.paddingTop = 10;
                wrap.style.paddingBottom = 10;
                wrap.style.marginTop = 4;
                wrap.style.borderTopLeftRadius = 6;
                wrap.style.borderTopRightRadius = 6;
                wrap.style.borderBottomLeftRadius = 6;
                wrap.style.borderBottomRightRadius = 6;

                foreach (var line in json.Split('\n'))
                {
                    var l = new Label(string.IsNullOrEmpty(line) ? " " : line);
                    l.style.color = TextHi;
                    l.style.fontSize = 12;
                    l.style.whiteSpace = WhiteSpace.Normal;
                    l.style.flexShrink = 0;
                    wrap.Add(l);
                }
                box.Add(wrap);

                var copy = new Label("Copy JSON");
                copy.style.color = TextHi;
                copy.style.backgroundColor = Bg2;
                copy.style.paddingLeft = 14;
                copy.style.paddingRight = 14;
                copy.style.paddingTop = 8;
                copy.style.paddingBottom = 8;
                copy.style.marginTop = 8;
                copy.style.fontSize = 12;
                copy.style.alignSelf = Align.FlexStart;
                copy.RegisterCallback<ClickEvent>(evt =>
                {
                    GUIUtility.systemCopyBuffer = json;
                    ShowToast($"Copied {json.Length} chars");
                    evt.StopPropagation();
                });
                box.Add(copy);
            }

            return box;
        }
    }
}
