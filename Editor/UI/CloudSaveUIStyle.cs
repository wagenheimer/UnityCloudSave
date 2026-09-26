using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Wagenheimer.CloudSave.Editor.UI
{
    internal static class CloudSaveUIStyle
    {
        public const string PackageStylePath = "Packages/com.wagenheimer.cloudsave/Editor/UI/CloudSaveCommon.uss";

        public static readonly Color ColBgDark = new Color(0.08f, 0.09f, 0.12f);       // #14171F
        public static readonly Color ColCardBg = new Color(0.11f, 0.13f, 0.17f);       // #1C202B
        public static readonly Color ColCardBorder = new Color(0.18f, 0.21f, 0.28f);   // #2E3647
        public static readonly Color ColAccent = new Color(0.06f, 0.65f, 0.91f);       // #0EA5E9
        public static readonly Color ColTextWhite = new Color(0.95f, 0.96f, 0.98f);
        public static readonly Color ColTextMuted = new Color(0.58f, 0.64f, 0.72f);
        public static readonly Color ColGreen = new Color(0.10f, 0.73f, 0.51f);
        public static readonly Color ColAmber = new Color(0.96f, 0.62f, 0.04f);
        public static readonly Color ColRed = new Color(0.94f, 0.27f, 0.27f);

        public static void SetRadius(this IStyle s, float r)
        {
            s.borderTopLeftRadius = r;
            s.borderTopRightRadius = r;
            s.borderBottomLeftRadius = r;
            s.borderBottomRightRadius = r;
        }

        public static void Apply(VisualElement element)
        {
            if (element == null) return;

            StyleSheet sheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(PackageStylePath);
            if (sheet == null)
            {
                var guids = AssetDatabase.FindAssets("CloudSaveCommon t:StyleSheet");
                if (guids != null && guids.Length > 0)
                {
                    var path = AssetDatabase.GUIDToAssetPath(guids[0]);
                    sheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(path);
                }
            }

            if (sheet != null && !element.styleSheets.Contains(sheet))
            {
                element.styleSheets.Add(sheet);
            }
        }

        public static VisualElement CreateHeader(string title, string subtitle, string version, Action onCheckUpdates = null)
        {
            var header = new VisualElement();
            header.AddToClassList("cs-header");
            header.style.backgroundColor = new StyleColor(ColCardBg);
            header.style.borderTopWidth = 1;
            header.style.borderRightWidth = 1;
            header.style.borderBottomWidth = 1;
            header.style.borderLeftWidth = 4;
            header.style.borderTopColor = new StyleColor(ColCardBorder);
            header.style.borderRightColor = new StyleColor(ColCardBorder);
            header.style.borderBottomColor = new StyleColor(ColCardBorder);
            header.style.borderLeftColor = new StyleColor(ColAccent);
            header.style.SetRadius(8);
            header.style.paddingTop = 14;
            header.style.paddingBottom = 14;
            header.style.paddingLeft = 18;
            header.style.paddingRight = 18;
            header.style.marginBottom = 12;
            header.style.flexDirection = FlexDirection.Row;
            header.style.justifyContent = Justify.SpaceBetween;
            header.style.alignItems = Align.Center;

            var left = new VisualElement();
            left.style.flexDirection = FlexDirection.Column;

            var titleRow = new VisualElement();
            titleRow.style.flexDirection = FlexDirection.Row;
            titleRow.style.alignItems = Align.Center;

            var titleLbl = new Label(title);
            titleLbl.style.fontSize = 17;
            titleLbl.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleLbl.style.color = new StyleColor(ColTextWhite);
            titleRow.Add(titleLbl);

            var authorBadge = new Label("by Cezar Wagenheimer");
            authorBadge.style.backgroundColor = new StyleColor(new Color(0.06f, 0.65f, 0.91f, 0.15f));
            authorBadge.style.borderTopWidth = 1;
            authorBadge.style.borderBottomWidth = 1;
            authorBadge.style.borderLeftWidth = 1;
            authorBadge.style.borderRightWidth = 1;
            authorBadge.style.borderTopColor = new StyleColor(ColAccent);
            authorBadge.style.borderBottomColor = new StyleColor(ColAccent);
            authorBadge.style.borderLeftColor = new StyleColor(ColAccent);
            authorBadge.style.borderRightColor = new StyleColor(ColAccent);
            authorBadge.style.SetRadius(10);
            authorBadge.style.paddingTop = 2;
            authorBadge.style.paddingBottom = 2;
            authorBadge.style.paddingLeft = 8;
            authorBadge.style.paddingRight = 8;
            authorBadge.style.marginLeft = 10;
            authorBadge.style.fontSize = 10;
            authorBadge.style.unityFontStyleAndWeight = FontStyle.Bold;
            authorBadge.style.color = new StyleColor(ColAccent);
            titleRow.Add(authorBadge);

            if (!string.IsNullOrEmpty(version))
            {
                var verBadge = CreateBadge($"v{version}", "cs-badge-info");
                verBadge.style.marginLeft = 6;
                titleRow.Add(verBadge);
            }

            left.Add(titleRow);

            var subLbl = new Label(subtitle);
            subLbl.style.fontSize = 11;
            subLbl.style.color = new StyleColor(ColTextMuted);
            subLbl.style.marginTop = 3;
            left.Add(subLbl);

            header.Add(left);

            var right = new VisualElement();
            right.style.flexDirection = FlexDirection.Row;
            right.style.alignItems = Align.Center;

            if (onCheckUpdates != null)
            {
                var updateBtn = CreateButton("Check for Updates", "cs-btn-secondary", onCheckUpdates);
                updateBtn.style.height = 28;
                right.Add(updateBtn);
            }

            header.Add(right);
            return header;
        }

        public static VisualElement CreateMetricCard(string label, string initialValue, out Label valLbl, Color? accentColor = null)
        {
            var card = new VisualElement();
            card.AddToClassList("cs-metric-card");
            card.style.flexGrow = 1;
            card.style.backgroundColor = new StyleColor(ColCardBg);
            card.style.borderTopWidth = 1;
            card.style.borderBottomWidth = 1;
            card.style.borderLeftWidth = 1;
            card.style.borderRightWidth = 1;
            card.style.borderTopColor = new StyleColor(ColCardBorder);
            card.style.borderBottomColor = new StyleColor(ColCardBorder);
            card.style.borderLeftColor = new StyleColor(ColCardBorder);
            card.style.borderRightColor = new StyleColor(ColCardBorder);
            card.style.SetRadius(7);
            card.style.paddingTop = 8;
            card.style.paddingBottom = 8;
            card.style.paddingLeft = 12;
            card.style.paddingRight = 12;
            card.style.marginRight = 8;
            card.style.alignItems = Align.Center;

            valLbl = new Label(initialValue);
            valLbl.style.fontSize = 18;
            valLbl.style.unityFontStyleAndWeight = FontStyle.Bold;
            valLbl.style.color = new StyleColor(accentColor ?? ColTextWhite);

            var lbl = new Label(label);
            lbl.style.fontSize = 10;
            lbl.style.color = new StyleColor(ColTextMuted);
            lbl.style.marginTop = 2;
            lbl.style.unityTextAlign = TextAnchor.MiddleCenter;

            card.Add(valLbl);
            card.Add(lbl);
            return card;
        }

        public static VisualElement CreateCard(string title, string subtitle = null)
        {
            var card = new VisualElement();
            card.AddToClassList("cs-card");
            card.style.backgroundColor = new StyleColor(ColCardBg);
            card.style.borderTopWidth = 1;
            card.style.borderBottomWidth = 1;
            card.style.borderLeftWidth = 1;
            card.style.borderRightWidth = 1;
            card.style.borderTopColor = new StyleColor(ColCardBorder);
            card.style.borderBottomColor = new StyleColor(ColCardBorder);
            card.style.borderLeftColor = new StyleColor(ColCardBorder);
            card.style.borderRightColor = new StyleColor(ColCardBorder);
            card.style.SetRadius(8);
            card.style.paddingTop = 12;
            card.style.paddingBottom = 12;
            card.style.paddingLeft = 14;
            card.style.paddingRight = 14;
            card.style.marginBottom = 10;

            if (!string.IsNullOrEmpty(title))
            {
                var titleLbl = new Label(title);
                titleLbl.style.fontSize = 13.5f;
                titleLbl.style.unityFontStyleAndWeight = FontStyle.Bold;
                titleLbl.style.color = new StyleColor(ColTextWhite);
                titleLbl.style.marginBottom = 2;
                card.Add(titleLbl);
            }

            if (!string.IsNullOrEmpty(subtitle))
            {
                var subLbl = new Label(subtitle);
                subLbl.style.fontSize = 11;
                subLbl.style.color = new StyleColor(ColTextMuted);
                subLbl.style.marginBottom = 8;
                subLbl.style.whiteSpace = WhiteSpace.Normal;
                card.Add(subLbl);
            }

            return card;
        }

        public static Label CreateBadge(string text, string typeClass = "cs-badge-info")
        {
            var badge = new Label(text);
            badge.AddToClassList("cs-badge");
            badge.style.paddingTop = 3;
            badge.style.paddingBottom = 3;
            badge.style.paddingLeft = 8;
            badge.style.paddingRight = 8;
            badge.style.SetRadius(12);
            badge.style.fontSize = 10;
            badge.style.unityFontStyleAndWeight = FontStyle.Bold;
            badge.style.borderTopWidth = 1;
            badge.style.borderBottomWidth = 1;
            badge.style.borderLeftWidth = 1;
            badge.style.borderRightWidth = 1;

            if (typeClass.Contains("pass"))
            {
                badge.style.backgroundColor = new StyleColor(new Color(0.10f, 0.73f, 0.51f, 0.15f));
                badge.style.borderTopColor = new StyleColor(ColGreen);
                badge.style.borderBottomColor = new StyleColor(ColGreen);
                badge.style.borderLeftColor = new StyleColor(ColGreen);
                badge.style.borderRightColor = new StyleColor(ColGreen);
                badge.style.color = new StyleColor(new Color(0.43f, 0.91f, 0.72f));
            }
            else if (typeClass.Contains("warn") || typeClass.Contains("update"))
            {
                badge.style.backgroundColor = new StyleColor(new Color(0.96f, 0.62f, 0.04f, 0.18f));
                badge.style.borderTopColor = new StyleColor(ColAmber);
                badge.style.borderBottomColor = new StyleColor(ColAmber);
                badge.style.borderLeftColor = new StyleColor(ColAmber);
                badge.style.borderRightColor = new StyleColor(ColAmber);
                badge.style.color = new StyleColor(new Color(0.99f, 0.83f, 0.30f));
            }
            else if (typeClass.Contains("fail"))
            {
                badge.style.backgroundColor = new StyleColor(new Color(0.94f, 0.27f, 0.27f, 0.18f));
                badge.style.borderTopColor = new StyleColor(ColRed);
                badge.style.borderBottomColor = new StyleColor(ColRed);
                badge.style.borderLeftColor = new StyleColor(ColRed);
                badge.style.borderRightColor = new StyleColor(ColRed);
                badge.style.color = new StyleColor(new Color(0.99f, 0.60f, 0.60f));
            }
            else
            {
                badge.style.backgroundColor = new StyleColor(new Color(0.06f, 0.65f, 0.91f, 0.15f));
                badge.style.borderTopColor = new StyleColor(ColAccent);
                badge.style.borderBottomColor = new StyleColor(ColAccent);
                badge.style.borderLeftColor = new StyleColor(ColAccent);
                badge.style.borderRightColor = new StyleColor(ColAccent);
                badge.style.color = new StyleColor(new Color(0.49f, 0.83f, 0.99f));
            }

            if (!string.IsNullOrEmpty(typeClass))
            {
                badge.AddToClassList(typeClass);
            }
            return badge;
        }

        public static Button CreateButton(string text, string styleClass, Action onClick)
        {
            var btn = new Button(onClick) { text = text };
            btn.AddToClassList("cs-btn");
            btn.style.SetRadius(5);
            btn.style.paddingTop = 5;
            btn.style.paddingBottom = 5;
            btn.style.paddingLeft = 12;
            btn.style.paddingRight = 12;
            btn.style.fontSize = 11;
            btn.style.unityFontStyleAndWeight = FontStyle.Bold;
            btn.style.borderTopWidth = 1;
            btn.style.borderBottomWidth = 1;
            btn.style.borderLeftWidth = 1;
            btn.style.borderRightWidth = 1;
            btn.style.borderTopColor = new StyleColor(new Color(0.22f, 0.26f, 0.34f));
            btn.style.borderBottomColor = new StyleColor(new Color(0.22f, 0.26f, 0.34f));
            btn.style.borderLeftColor = new StyleColor(new Color(0.22f, 0.26f, 0.34f));
            btn.style.borderRightColor = new StyleColor(new Color(0.22f, 0.26f, 0.34f));
            btn.style.backgroundColor = new StyleColor(new Color(0.14f, 0.16f, 0.22f));
            btn.style.color = new StyleColor(ColTextWhite);

            if (styleClass != null && styleClass.Contains("primary"))
            {
                btn.style.backgroundColor = new StyleColor(new Color(0.01f, 0.52f, 0.78f));
                btn.style.borderTopColor = new StyleColor(ColAccent);
                btn.style.borderBottomColor = new StyleColor(ColAccent);
                btn.style.borderLeftColor = new StyleColor(ColAccent);
                btn.style.borderRightColor = new StyleColor(ColAccent);
            }
            else if (styleClass != null && styleClass.Contains("success"))
            {
                btn.style.backgroundColor = new StyleColor(new Color(0.02f, 0.59f, 0.41f));
                btn.style.borderTopColor = new StyleColor(ColGreen);
                btn.style.borderBottomColor = new StyleColor(ColGreen);
                btn.style.borderLeftColor = new StyleColor(ColGreen);
                btn.style.borderRightColor = new StyleColor(ColGreen);
            }
            else if (styleClass != null && styleClass.Contains("warning"))
            {
                btn.style.backgroundColor = new StyleColor(new Color(0.85f, 0.47f, 0.02f));
                btn.style.borderTopColor = new StyleColor(ColAmber);
                btn.style.borderBottomColor = new StyleColor(ColAmber);
                btn.style.borderLeftColor = new StyleColor(ColAmber);
                btn.style.borderRightColor = new StyleColor(ColAmber);
            }

            if (!string.IsNullOrEmpty(styleClass))
            {
                btn.AddToClassList(styleClass);
            }
            return btn;
        }
    }
}
