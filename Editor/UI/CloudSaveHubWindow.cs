using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Wagenheimer.CloudSave.Editor.Setup;
using Wagenheimer.CloudSave.Verification;
using Wagenheimer.PackageHub.Editor;

namespace Wagenheimer.CloudSave.Editor.UI
{
    /// <summary>
    /// Master UI Toolkit Hub & Dashboard for Unity Cloud Save.
    /// Provides full diagnostics checklist, verification engine, interactive cloud tester, and integration guides.
    /// </summary>
    public sealed class CloudSaveHubWindow : EditorWindow
    {
        public enum Tab
        {
            Diagnostics = 0,
            LiveQA = 1,
            Guide = 2,
            About = 3
        }

        private const string PackageJsonPath = "Packages/com.wagenheimer.cloudsave/package.json";
        private const string RepoUrl = "https://github.com/wagenheimer/UnityCloudSave";
        private const string IssuesUrl = "https://github.com/wagenheimer/UnityCloudSave/issues";

        private string _version = "4.26.0";
        private Tab _currentTab = Tab.Diagnostics;
        private VisualElement _contentContainer;
        private Button[] _tabButtons;

        // Diagnostics Cache
        public enum DiagnosticsViewMode
        {
            FourPillars = 0,
            FullChecklist = 1
        }

        public enum StepFilter
        {
            All = 0,
            IssuesOnly = 1,
            RequiredOnly = 2,
            ValidatedOnly = 3
        }

        private DiagnosticsViewMode _diagViewMode = DiagnosticsViewMode.FourPillars;
        private StepFilter _stepFilter = StepFilter.All;

        private SetupRegistry _registry;
        private SetupContext _ctx;
        private CloudSaveSetupState _state;
        private SetupSnapshot _snapshot;
        private readonly HashSet<string> _expandedSteps = new();
        private Vector2 _diagnosticsScrollOffset = Vector2.zero;
        private bool _isBusy;

        // Tester state
        private string _testKey = "PlayerSave";
        private string _testValue = "{\"coins\":100,\"level\":5}";
        private string _testConsoleLog = "";

        // Main Entry point in Tools/Wagenheimer (Priority 50 places it in its own clean separated block)
        [MenuItem("Tools/Wagenheimer/Cloud Save/Dashboard...", priority = 50)]
        public static void OpenDashboard()
        {
            Open(Tab.Diagnostics);
        }

        [MenuItem("Tools/Wagenheimer/Cloud Save/Cloud Tester...", priority = 51)]
        public static void OpenTester()
        {
            Open(Tab.LiveQA);
        }

        [MenuItem("Window/Wagenheimer/Cloud Save/Dashboard", priority = 200)]
        public static void OpenWindowAlt()
        {
            Open(Tab.Diagnostics);
        }

        public static CloudSaveHubWindow Open(Tab tab = Tab.Diagnostics)
        {
            var window = GetWindow<CloudSaveHubWindow>("Cloud Save");
            window.minSize = new Vector2(700, 560);
            window.SwitchTab(tab);
            window.Show();
            window.Focus();
            return window;
        }

        private void OnEnable()
        {
            LoadPackageVersion();
            _registry = new SetupRegistry();
            RecomputeDiagnostics();
        }

        private void CreateGUI()
        {
            rootVisualElement.Clear();
            rootVisualElement.style.backgroundColor = new StyleColor(CloudSaveUIStyle.ColBgDark);
            CloudSaveUIStyle.Apply(rootVisualElement);

            var root = new VisualElement();
            root.AddToClassList("cs-root");
            root.style.flexGrow = 1;
            root.style.backgroundColor = new StyleColor(CloudSaveUIStyle.ColBgDark);
            root.style.paddingTop = 12;
            root.style.paddingBottom = 12;
            root.style.paddingLeft = 16;
            root.style.paddingRight = 16;

            // Header Banner
            var header = CloudSaveUIStyle.CreateHeader(
                "Cloud Save",
                "Cross-Platform UGS Cloud Save, Authentication & Conflict Resolution Suite",
                _version,
                () => PackageHubWindow.OpenToPackage("com.wagenheimer.cloudsave")
            );
            root.Add(header);

            // Tab Bar
            var tabToolbar = new VisualElement();
            tabToolbar.AddToClassList("cs-tab-bar");
            tabToolbar.style.flexDirection = FlexDirection.Row;
            tabToolbar.style.backgroundColor = new StyleColor(CloudSaveUIStyle.ColCardBg);
            tabToolbar.style.borderTopWidth = 1;
            tabToolbar.style.borderBottomWidth = 1;
            tabToolbar.style.borderLeftWidth = 1;
            tabToolbar.style.borderRightWidth = 1;
            tabToolbar.style.borderTopColor = new StyleColor(CloudSaveUIStyle.ColCardBorder);
            tabToolbar.style.borderBottomColor = new StyleColor(CloudSaveUIStyle.ColCardBorder);
            tabToolbar.style.borderLeftColor = new StyleColor(CloudSaveUIStyle.ColCardBorder);
            tabToolbar.style.borderRightColor = new StyleColor(CloudSaveUIStyle.ColCardBorder);
            tabToolbar.style.SetRadius(7);
            tabToolbar.style.paddingTop = 3;
            tabToolbar.style.paddingBottom = 3;
            tabToolbar.style.paddingLeft = 3;
            tabToolbar.style.paddingRight = 3;
            tabToolbar.style.marginBottom = 12;

            var tabNames = new[] { "Setup & Diagnostics", "Live QA & Tester", "Integration Guide", "About & Updates" };
            _tabButtons = new Button[tabNames.Length];

            for (var i = 0; i < tabNames.Length; i++)
            {
                var tabIndex = (Tab)i;
                var btn = new Button(() => SwitchTab(tabIndex))
                {
                    text = tabNames[i]
                };
                btn.AddToClassList("cs-tab-btn");
                btn.style.flexGrow = 1;
                btn.style.height = 30;
                btn.style.borderTopWidth = 0;
                btn.style.borderBottomWidth = 0;
                btn.style.borderLeftWidth = 0;
                btn.style.borderRightWidth = 0;
                btn.style.SetRadius(5);
                btn.style.backgroundColor = new StyleColor(Color.clear);
                btn.style.color = new StyleColor(CloudSaveUIStyle.ColTextMuted);
                btn.style.fontSize = 11.5f;
                btn.style.unityFontStyleAndWeight = FontStyle.Bold;
                btn.style.marginLeft = 2;
                btn.style.marginRight = 2;
                _tabButtons[i] = btn;
                tabToolbar.Add(btn);
            }
            root.Add(tabToolbar);

            // Dynamic Content Area
            _contentContainer = new VisualElement();
            _contentContainer.style.flexGrow = 1;
            root.Add(_contentContainer);

            rootVisualElement.Add(root);

            RenderActiveTab();
        }

        public void SwitchTab(Tab tab)
        {
            _currentTab = tab;
            RenderActiveTab();
        }

        private void RenderActiveTab()
        {
            if (_contentContainer == null) return;
            var existingScroll = _contentContainer.Q<ScrollView>();
            if (existingScroll != null && _currentTab == Tab.Diagnostics)
            {
                _diagnosticsScrollOffset = existingScroll.scrollOffset;
            }
            _contentContainer.Clear();

            if (_tabButtons != null)
            {
                for (var i = 0; i < _tabButtons.Length; i++)
                {
                    if (i == (int)_currentTab)
                    {
                        _tabButtons[i].AddToClassList("cs-tab-btn--active");
                        _tabButtons[i].style.backgroundColor = new StyleColor(CloudSaveUIStyle.ColAccent);
                        _tabButtons[i].style.color = new StyleColor(Color.white);
                    }
                    else
                    {
                        _tabButtons[i].RemoveFromClassList("cs-tab-btn--active");
                        _tabButtons[i].style.backgroundColor = new StyleColor(Color.clear);
                        _tabButtons[i].style.color = new StyleColor(CloudSaveUIStyle.ColTextMuted);
                    }
                }
            }

            switch (_currentTab)
            {
                case Tab.Diagnostics:
                    _contentContainer.Add(BuildDiagnosticsView());
                    break;
                case Tab.LiveQA:
                    _contentContainer.Add(BuildLiveQAView());
                    break;
                case Tab.Guide:
                    _contentContainer.Add(BuildGuideView());
                    break;
                case Tab.About:
                    _contentContainer.Add(BuildAboutView());
                    break;
            }
        }

        #region Tab 0: Setup & Diagnostics (CHECKER)

        private VisualElement BuildDiagnosticsView()
        {
            var scroll = new ScrollView();
            scroll.style.flexGrow = 1;
            if (_diagnosticsScrollOffset != Vector2.zero)
            {
                scroll.schedule.Execute(() => scroll.scrollOffset = _diagnosticsScrollOffset);
            }

            if (_snapshot == null)
            {
                RecomputeDiagnostics();
            }

            // 1. Metric Meters Row
            var metricsRow = new VisualElement();
            metricsRow.AddToClassList("cs-metrics-row");
            metricsRow.style.flexDirection = FlexDirection.Row;
            metricsRow.style.marginBottom = 10;

            var integFraction = _snapshot?.Integration.ToString() ?? "0/0";
            var verifFraction = _snapshot?.Verification.ToString() ?? "0/0";

            var (readinessText, readinessCol) = _snapshot?.Readiness switch
            {
                ReadinessVerdict.Green => ("PRONTO ✔", CloudSaveUIStyle.ColGreen),
                ReadinessVerdict.Amber => ("QUASE LÁ ⚠️", CloudSaveUIStyle.ColAmber),
                _ => ("PENDENTE 🔴", CloudSaveUIStyle.ColRed)
            };

            metricsRow.Add(CloudSaveUIStyle.CreateMetricCard("Estrutura / Setup", integFraction, out var intLbl, CloudSaveUIStyle.ColAccent));
            metricsRow.Add(CloudSaveUIStyle.CreateMetricCard("Validações Concluídas", verifFraction, out var verLbl, CloudSaveUIStyle.ColGreen));
            metricsRow.Add(CloudSaveUIStyle.CreateMetricCard("Status de Produção", readinessText, out var readLbl, readinessCol));

            scroll.Add(metricsRow);

            // 2. View Mode Selector (Sub-toolbar: 4 Pilares vs Checklist Técnico)
            var modeBar = new VisualElement();
            modeBar.style.flexDirection = FlexDirection.Row;
            modeBar.style.backgroundColor = new StyleColor(CloudSaveUIStyle.ColCardBg);
            modeBar.style.borderTopWidth = 1;
            modeBar.style.borderBottomWidth = 1;
            modeBar.style.borderLeftWidth = 1;
            modeBar.style.borderRightWidth = 1;
            modeBar.style.borderTopColor = new StyleColor(CloudSaveUIStyle.ColCardBorder);
            modeBar.style.borderBottomColor = new StyleColor(CloudSaveUIStyle.ColCardBorder);
            modeBar.style.borderLeftColor = new StyleColor(CloudSaveUIStyle.ColCardBorder);
            modeBar.style.borderRightColor = new StyleColor(CloudSaveUIStyle.ColCardBorder);
            modeBar.style.SetRadius(6);
            modeBar.style.paddingTop = 3;
            modeBar.style.paddingBottom = 3;
            modeBar.style.paddingLeft = 3;
            modeBar.style.paddingRight = 3;
            modeBar.style.marginBottom = 12;

            var btnPillars = new Button(() =>
            {
                _diagViewMode = DiagnosticsViewMode.FourPillars;
                RenderActiveTab();
            })
            {
                text = "🌟 Visão Geral dos 4 Pilares (Fluxo Recomendado)"
            };
            btnPillars.style.flexGrow = 1;
            btnPillars.style.height = 28;
            btnPillars.style.fontSize = 11;
            btnPillars.style.unityFontStyleAndWeight = FontStyle.Bold;
            btnPillars.style.borderTopWidth = 0;
            btnPillars.style.borderBottomWidth = 0;
            btnPillars.style.borderLeftWidth = 0;
            btnPillars.style.borderRightWidth = 0;
            btnPillars.style.SetRadius(4);

            var btnChecklist = new Button(() =>
            {
                _diagViewMode = DiagnosticsViewMode.FullChecklist;
                RenderActiveTab();
            })
            {
                text = "📋 Checklist Técnico Detalhado (Todas as Categorias)"
            };
            btnChecklist.style.flexGrow = 1;
            btnChecklist.style.height = 28;
            btnChecklist.style.fontSize = 11;
            btnChecklist.style.unityFontStyleAndWeight = FontStyle.Bold;
            btnChecklist.style.borderTopWidth = 0;
            btnChecklist.style.borderBottomWidth = 0;
            btnChecklist.style.borderLeftWidth = 0;
            btnChecklist.style.borderRightWidth = 0;
            btnChecklist.style.SetRadius(4);

            if (_diagViewMode == DiagnosticsViewMode.FourPillars)
            {
                btnPillars.style.backgroundColor = new StyleColor(CloudSaveUIStyle.ColAccent);
                btnPillars.style.color = new StyleColor(Color.white);
                btnChecklist.style.backgroundColor = new StyleColor(Color.clear);
                btnChecklist.style.color = new StyleColor(CloudSaveUIStyle.ColTextMuted);
            }
            else
            {
                btnChecklist.style.backgroundColor = new StyleColor(CloudSaveUIStyle.ColAccent);
                btnChecklist.style.color = new StyleColor(Color.white);
                btnPillars.style.backgroundColor = new StyleColor(Color.clear);
                btnPillars.style.color = new StyleColor(CloudSaveUIStyle.ColTextMuted);
            }

            modeBar.Add(btnPillars);
            modeBar.Add(btnChecklist);
            scroll.Add(modeBar);

            // 3. Render Mode
            if (_diagViewMode == DiagnosticsViewMode.FourPillars)
            {
                scroll.Add(BuildFourPillarsView());
            }
            else
            {
                scroll.Add(BuildChecklistView());
            }

            return scroll;
        }

        private VisualElement BuildFourPillarsView()
        {
            var container = new VisualElement();

            if (_snapshot == null) return container;

            // Pillar 1: Unity Cloud Services (UGS)
            var p1Steps = _snapshot.Steps.Where(s =>
                s.Definition.Id == SetupRegistry.Ids.UgsProject ||
                s.Definition.Id == SetupRegistry.Ids.CloudSaveService ||
                s.Definition.Id == SetupRegistry.Ids.AnonymousAuth ||
                s.Definition.Id == SetupRegistry.Ids.AnonymousVerification).ToList();

            var p1Status = ComputePillarStatus(p1Steps);
            var p1Items = new List<(string, bool, string)>
            {
                ("Projeto UGS Conectado", IsStepReady(SetupRegistry.Ids.UgsProject), "Vinculado à Organização no Project Settings > Services"),
                ("Pacote Cloud Save SDK", IsStepReady(SetupRegistry.Ids.CloudSaveService), "Biblioteca com.unity.services.cloudsave instalada"),
                ("Pacote Authentication SDK", IsStepReady(SetupRegistry.Ids.AnonymousAuth), "Biblioteca com.unity.services.authentication instalada"),
                ("Login Anônimo Testado", IsStepReady(SetupRegistry.Ids.AnonymousVerification), "Teste de autenticação e obtenção de Player ID estável")
            };

            var p1Actions = new List<(string, string, Action)>
            {
                ("⚙️ Abrir Project Settings", "cs-btn-primary", () => SettingsService.OpenProjectSettings("Project/Services")),
                ("🧪 Testar Login Anônimo", "cs-btn-secondary", () =>
                {
                    var step = _snapshot.Steps.FirstOrDefault(s => s.Definition.Id == SetupRegistry.Ids.AnonymousVerification);
                    if (step != null) RunValidation(step);
                }),
                ("🔍 Ver no Checklist", "cs-btn-secondary", () => SwitchToChecklistAndExpand(SetupRegistry.Ids.UgsProject))
            };

            container.Add(BuildPillarCard(
                "1",
                "Conexão Unity Gaming Services (UGS)",
                "A infraestrutura de nuvem base. Conecta seu jogo aos servidores da Unity para armazenamento e autenticação.",
                p1Status.statusText,
                p1Status.badgeClass,
                p1Status.color,
                p1Items,
                p1Actions
            ));

            // Pillar 2: Game Code & Save Architecture
            var p2Steps = _snapshot.Steps.Where(s =>
                s.Definition.Id == SetupRegistry.Ids.CodeController ||
                s.Definition.Id == SetupRegistry.Ids.CodeConfigure ||
                s.Definition.Id == SetupRegistry.Ids.CodeInitSync ||
                s.Definition.Id == SetupRegistry.Ids.CodeSave ||
                s.Definition.Id == SetupRegistry.Ids.SaveSerializable ||
                s.Definition.Id == SetupRegistry.Ids.SaveTimestamp).ToList();

            var p2Status = ComputePillarStatus(p2Steps);
            bool usesController = IsStepReady(SetupRegistry.Ids.CodeController);
            var p2Items = new List<(string, bool, string)>
            {
                (usesController ? "CloudSaveController (Recomendado)" : "Inicialização CloudSync", usesController || IsStepReady(SetupRegistry.Ids.CodeConfigure), usesController ? "Fluxo automático de sincronia ativo" : "Chamada CloudSync.Configure / InitAndSyncAsync"),
                ("Chamada de Salvamento em Nuvem", usesController || IsStepReady(SetupRegistry.Ids.CodeSave), usesController ? "Acionado via MarkDirty() ou StartAsync()" : "Chamada CloudSync.SaveAsync() após saves locais"),
                ("Classe de Dados Serializável", IsStepReady(SetupRegistry.Ids.SaveSerializable), "Classe decorada com [System.Serializable] para JsonUtility"),
                ("Timestamp de Conflito", usesController || IsStepReady(SetupRegistry.Ids.SaveTimestamp), usesController ? "Gerenciado automaticamente pelo Controller" : "Campo long LastSaved / SaveDateTime")
            };

            var p2Actions = new List<(string, string, Action)>
            {
                ("📋 Copiar Código Recomendado", "cs-btn-primary", CopyControllerSnippet),
                ("🔍 Ver no Checklist", "cs-btn-secondary", () => SwitchToChecklistAndExpand(SetupRegistry.Ids.CodeController))
            };

            container.Add(BuildPillarCard(
                "2",
                "Integração no Código do Jogo",
                "Como seu jogo lê e grava na nuvem. O CloudSaveController cuida de sincronia, conflitos e inicialização automaticamente.",
                p2Status.statusText,
                p2Status.badgeClass,
                p2Status.color,
                p2Items,
                p2Actions
            ));

            // Pillar 3: Mobile Identity Providers
            var p3Steps = _snapshot.Steps.Where(s =>
                s.Definition.Id == SetupRegistry.Ids.ProviderAndroid ||
                s.Definition.Id == SetupRegistry.Ids.ProviderApple ||
                s.Definition.Id == SetupRegistry.Ids.CodeAuthUpgrade).ToList();

            var p3Status = ComputePillarStatus(p3Steps);
            var p3Items = new List<(string, bool, string)>
            {
                ("Google Play Games (Android)", IsStepReady(SetupRegistry.Ids.ProviderAndroid), "Login nativo silencioso (Game Service, NÃO web OAuth)"),
                ("Apple Game Center (iOS)", IsStepReady(SetupRegistry.Ids.ProviderApple), "Banner nativo Game Center e vínculo de conta"),
                ("Vínculo de Contas no Código", IsStepReady(SetupRegistry.Ids.CodeAuthUpgrade), "Suporte a CloudAuth.Link*Async para persistência cross-device")
            };

            var p3Actions = new List<(string, string, Action)>
            {
                ("🌐 Unity Dashboard (Providers)", "cs-btn-primary", () => Application.OpenURL("https://cloud.unity.com/")),
                ("💡 Dicas Game Services vs Web", "cs-btn-secondary", () =>
                {
                    EditorUtility.DisplayDialog(
                        "Configuração Mobile Recomendada",
                        "Para jogos mobile, NUNCA use provedores web com tela de login:\n\n" +
                        "1. Android: Escolha 'Google Play Games' (NÃO escolha 'Google').\n" +
                        "   - Login 100% silencioso e automático.\n" +
                        "   - Sincroniza com as Conquistas (Achievements).\n\n" +
                        "2. iOS: Escolha 'Apple Game Center' (NÃO escolha 'Sign in with Apple').\n" +
                        "   - Banner nativo Game Center no topo da tela.\n" +
                        "   - Sem necessidade de tela de login web.",
                        "Entendido"
                    );
                }),
                ("🔍 Ver no Checklist", "cs-btn-secondary", () => SwitchToChecklistAndExpand(SetupRegistry.Ids.ProviderAndroid))
            };

            container.Add(BuildPillarCard(
                "3",
                "Provedores Mobile (Google Play & Apple)",
                "Permite que o jogador jogue no celular, troque de aparelho ou reinstale e continue de onde parou sem telas de login chatas.",
                p3Status.statusText,
                p3Status.badgeClass,
                p3Status.color,
                p3Items,
                p3Actions
            ));

            // Pillar 4: Store Compliance & Safety
            var p4Steps = _snapshot.Steps.Where(s =>
                s.Definition.Id == SetupRegistry.Ids.ComplianceDeletion ||
                s.Definition.Id == SetupRegistry.Ids.ComplianceDeletionUi ||
                s.Definition.Id == SetupRegistry.Ids.ComplianceUrls).ToList();

            var p4Status = ComputePillarStatus(p4Steps);
            var p4Items = new List<(string, bool, string)>
            {
                ("Exclusão de Conta in-app (DeleteAccountAsync)", IsStepReady(SetupRegistry.Ids.ComplianceDeletion), "Apple Guideline 5.1.1(v) & Google Play: obrigatório para aprovação"),
                ("Interface de Confirmação de Exclusão", IsStepReady(SetupRegistry.Ids.ComplianceDeletionUi), "Botão acessível em configurações com diálogo de confirmação"),
                ("URLs de Privacidade e Exclusão", IsStepReady(SetupRegistry.Ids.ComplianceUrls), "Links de Política de Privacidade acessíveis no app")
            };

            var p4Actions = new List<(string, string, Action)>
            {
                ("📦 Gerar UI Prefab Deletar Conta", "cs-btn-primary", () =>
                {
                    CloudSaveUIPrefabGenerator.GenerateAccountDeletionUI();
                    RecomputeDiagnostics();
                    RenderActiveTab();
                }),
                ("📌 Inserir UI na Cena Atual", "cs-btn-secondary", () =>
                {
                    CloudSaveUIPrefabGenerator.CreateAccountDeletionUIInScene();
                }),
                ("🔍 Ver no Checklist", "cs-btn-secondary", () => SwitchToChecklistAndExpand(SetupRegistry.Ids.ComplianceDeletion))
            };

            container.Add(BuildPillarCard(
                "4",
                "Conformidade das Lojas (Store Compliance)",
                "Exigências obrigatórias para ter o jogo aprovado pela Apple e Google. Oferecer exclusão de conta e termos de privacidade.",
                p4Status.statusText,
                p4Status.badgeClass,
                p4Status.color,
                p4Items,
                p4Actions
            ));

            return container;
        }

        private VisualElement BuildPillarCard(
            string number,
            string title,
            string description,
            string statusText,
            string badgeClass,
            Color statusColor,
            List<(string label, bool ok, string detail)> items,
            List<(string btnText, string btnClass, Action onClick)> actions)
        {
            var card = new VisualElement();
            card.style.backgroundColor = new StyleColor(CloudSaveUIStyle.ColCardBg);
            card.style.borderTopWidth = 1;
            card.style.borderBottomWidth = 1;
            card.style.borderRightWidth = 1;
            card.style.borderLeftWidth = 4;
            card.style.borderTopColor = new StyleColor(CloudSaveUIStyle.ColCardBorder);
            card.style.borderBottomColor = new StyleColor(CloudSaveUIStyle.ColCardBorder);
            card.style.borderRightColor = new StyleColor(CloudSaveUIStyle.ColCardBorder);
            card.style.borderLeftColor = new StyleColor(statusColor);
            card.style.SetRadius(8);
            card.style.paddingTop = 12;
            card.style.paddingBottom = 12;
            card.style.paddingLeft = 14;
            card.style.paddingRight = 14;
            card.style.marginBottom = 12;

            // Header
            var headerRow = new VisualElement();
            headerRow.style.flexDirection = FlexDirection.Row;
            headerRow.style.justifyContent = Justify.SpaceBetween;
            headerRow.style.alignItems = Align.Center;
            headerRow.style.marginBottom = 4;

            var titleGroup = new VisualElement();
            titleGroup.style.flexDirection = FlexDirection.Row;
            titleGroup.style.alignItems = Align.Center;

            var numCircle = new Label(number);
            numCircle.style.width = 22;
            numCircle.style.height = 22;
            numCircle.style.backgroundColor = new StyleColor(statusColor);
            numCircle.style.color = new StyleColor(Color.white);
            numCircle.style.fontSize = 12;
            numCircle.style.unityFontStyleAndWeight = FontStyle.Bold;
            numCircle.style.unityTextAlign = TextAnchor.MiddleCenter;
            numCircle.style.SetRadius(11);
            numCircle.style.marginRight = 10;
            titleGroup.Add(numCircle);

            var titleLbl = new Label(title);
            titleLbl.style.fontSize = 13.5f;
            titleLbl.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleLbl.style.color = new StyleColor(CloudSaveUIStyle.ColTextWhite);
            titleGroup.Add(titleLbl);
            headerRow.Add(titleGroup);

            var badge = CloudSaveUIStyle.CreateBadge(statusText, badgeClass);
            headerRow.Add(badge);
            card.Add(headerRow);

            var descLbl = new Label(description);
            descLbl.style.fontSize = 11;
            descLbl.style.color = new StyleColor(CloudSaveUIStyle.ColTextMuted);
            descLbl.style.whiteSpace = WhiteSpace.Normal;
            descLbl.style.marginBottom = 10;
            descLbl.style.marginLeft = 32;
            card.Add(descLbl);

            // Item Checklist
            var itemsBox = new VisualElement();
            itemsBox.style.backgroundColor = new StyleColor(new Color(0.08f, 0.09f, 0.12f, 0.6f));
            itemsBox.style.borderTopWidth = 1;
            itemsBox.style.borderBottomWidth = 1;
            itemsBox.style.borderLeftWidth = 1;
            itemsBox.style.borderRightWidth = 1;
            itemsBox.style.borderTopColor = new StyleColor(new Color(1f, 1f, 1f, 0.05f));
            itemsBox.style.borderBottomColor = new StyleColor(new Color(1f, 1f, 1f, 0.05f));
            itemsBox.style.borderLeftColor = new StyleColor(new Color(1f, 1f, 1f, 0.05f));
            itemsBox.style.borderRightColor = new StyleColor(new Color(1f, 1f, 1f, 0.05f));
            itemsBox.style.SetRadius(6);
            itemsBox.style.paddingTop = 6;
            itemsBox.style.paddingBottom = 6;
            itemsBox.style.paddingLeft = 10;
            itemsBox.style.paddingRight = 10;
            itemsBox.style.marginBottom = 10;

            foreach (var (lbl, ok, detail) in items)
            {
                var itemRow = new VisualElement();
                itemRow.style.flexDirection = FlexDirection.Row;
                itemRow.style.alignItems = Align.Center;
                itemRow.style.paddingTop = 3;
                itemRow.style.paddingBottom = 3;

                var iconLbl = new Label(ok ? "✔" : "⚠️");
                iconLbl.style.fontSize = 11;
                iconLbl.style.unityFontStyleAndWeight = FontStyle.Bold;
                iconLbl.style.color = new StyleColor(ok ? CloudSaveUIStyle.ColGreen : CloudSaveUIStyle.ColAmber);
                iconLbl.style.marginRight = 8;
                itemRow.Add(iconLbl);

                var itemText = new Label(lbl);
                itemText.style.fontSize = 11;
                itemText.style.unityFontStyleAndWeight = FontStyle.Bold;
                itemText.style.color = new StyleColor(ok ? CloudSaveUIStyle.ColTextWhite : new Color(0.95f, 0.85f, 0.65f));
                itemRow.Add(itemText);

                if (!string.IsNullOrEmpty(detail))
                {
                    var detailLbl = new Label($"— {detail}");
                    detailLbl.style.fontSize = 10.5f;
                    detailLbl.style.color = new StyleColor(CloudSaveUIStyle.ColTextMuted);
                    detailLbl.style.marginLeft = 6;
                    itemRow.Add(detailLbl);
                }

                itemsBox.Add(itemRow);
            }
            card.Add(itemsBox);

            // Actions row
            var actionRow = new VisualElement();
            actionRow.style.flexDirection = FlexDirection.Row;
            actionRow.style.flexWrap = Wrap.Wrap;

            foreach (var (btnText, btnClass, onClick) in actions)
            {
                var btn = CloudSaveUIStyle.CreateButton(btnText, btnClass, onClick);
                btn.style.marginRight = 6;
                btn.style.marginBottom = 4;
                actionRow.Add(btn);
            }
            card.Add(actionRow);

            return card;
        }

        private VisualElement BuildChecklistView()
        {
            var container = new VisualElement();

            if (_snapshot == null) return container;

            // 1. Next Best Action Card
            if (_snapshot.NextAction != null)
            {
                var next = _snapshot.NextAction;
                var nextCard = CloudSaveUIStyle.CreateCard("AÇÃO RECOMENDADA A SEGUIR: " + next.Step.Definition.Title, next.Why);
                nextCard.style.borderLeftColor = new StyleColor(CloudSaveUIStyle.ColAccent);
                nextCard.style.borderLeftWidth = 3;

                var actionRow = new VisualElement();
                actionRow.style.flexDirection = FlexDirection.Row;
                actionRow.style.alignItems = Align.Center;
                actionRow.style.marginTop = 6;

                var showBtn = CloudSaveUIStyle.CreateButton("🔍 Inspecionar Passo", "cs-btn-primary", () =>
                {
                    _expandedSteps.Clear();
                    _expandedSteps.Add(next.Step.Definition.Id);
                    RenderActiveTab();
                });
                actionRow.Add(showBtn);

                if (next.Step.Definition.HasRuntimeValidator)
                {
                    var runBtn = CloudSaveUIStyle.CreateButton("🧪 Rodar Teste de Validação", "cs-btn-secondary", () =>
                    {
                        RunValidation(next.Step);
                    });
                    runBtn.style.marginLeft = 6;
                    actionRow.Add(runBtn);
                }

                nextCard.Add(actionRow);
                container.Add(nextCard);
            }

            // 2. Filter Pills Toolbar
            var allSteps = _snapshot.Steps.Where(e => e.State != StepState.NotApplicable).ToList();
            int totalCount = allSteps.Count;
            int issuesCount = allSteps.Count(e => e.State == StepState.Failed || e.State == StepState.NeedsAttention || e.State == StepState.NeedsValidation || e.State == StepState.Blocked);
            int reqCount = allSteps.Count(e => e.Definition.Obligation == Obligation.Required);
            int valCount = allSteps.Count(e => e.State == StepState.Validated || e.State == StepState.ManuallyConfirmed);

            var filterCard = CloudSaveUIStyle.CreateCard("Filtros do Checklist", $"Target atual: {EditorUserBuildSettings.activeBuildTarget}");

            var filterBar = new VisualElement();
            filterBar.style.flexDirection = FlexDirection.Row;
            filterBar.style.flexWrap = Wrap.Wrap;
            filterBar.style.marginBottom = 8;

            AddFilterButton(filterBar, $"Todos ({totalCount})", StepFilter.All, _stepFilter == StepFilter.All);
            AddFilterButton(filterBar, $"⚠️ Pendências & Atenção ({issuesCount})", StepFilter.IssuesOnly, _stepFilter == StepFilter.IssuesOnly, issuesCount > 0 ? CloudSaveUIStyle.ColAmber : (Color?)null);
            AddFilterButton(filterBar, $"🚨 Obrigatórios ({reqCount})", StepFilter.RequiredOnly, _stepFilter == StepFilter.RequiredOnly);
            AddFilterButton(filterBar, $"✔ Validados ({valCount})", StepFilter.ValidatedOnly, _stepFilter == StepFilter.ValidatedOnly, valCount > 0 ? CloudSaveUIStyle.ColGreen : (Color?)null);

            filterCard.Add(filterBar);

            // Controls sub-row
            var controlBar = new VisualElement();
            controlBar.style.flexDirection = FlexDirection.Row;
            controlBar.style.flexWrap = Wrap.Wrap;

            controlBar.Add(CloudSaveUIStyle.CreateButton("🔄 Re-Scan Diagnóstico", "cs-btn-primary", () =>
            {
                RecomputeDiagnostics();
                RenderActiveTab();
            }));

            controlBar.Add(CloudSaveUIStyle.CreateButton("📦 Gerar UI Prefabs", "cs-btn-secondary", () =>
            {
                CloudSaveUIPrefabGenerator.GenerateAll();
                RecomputeDiagnostics();
                RenderActiveTab();
            }));

            controlBar.Add(CloudSaveUIStyle.CreateButton("🛠️ Inserir Debug Overlay", "cs-btn-secondary", () =>
            {
                CloudSaveDebugOverlayEditor.AddDebugOverlayToScene();
            }));

            controlBar.Add(CloudSaveUIStyle.CreateButton("🤖 Copiar Relatório / Prompt AI", "cs-btn-secondary", CopyDiagnosticPrompt));

            controlBar.Add(CloudSaveUIStyle.CreateButton("▾ Expandir Todos", "cs-btn-secondary", () =>
            {
                foreach (var s in _snapshot.Steps) _expandedSteps.Add(s.Definition.Id);
                RenderActiveTab();
            }));

            controlBar.Add(CloudSaveUIStyle.CreateButton("▸ Recolher Todos", "cs-btn-secondary", () =>
            {
                _expandedSteps.Clear();
                RenderActiveTab();
            }));

            filterCard.Add(controlBar);
            container.Add(filterCard);

            // 3. Filtered Steps List
            var filteredSteps = allSteps.AsEnumerable();
            switch (_stepFilter)
            {
                case StepFilter.IssuesOnly:
                    filteredSteps = filteredSteps.Where(e => e.State == StepState.Failed || e.State == StepState.NeedsAttention || e.State == StepState.NeedsValidation || e.State == StepState.Blocked);
                    break;
                case StepFilter.RequiredOnly:
                    filteredSteps = filteredSteps.Where(e => e.Definition.Obligation == Obligation.Required);
                    break;
                case StepFilter.ValidatedOnly:
                    filteredSteps = filteredSteps.Where(e => e.State == StepState.Validated || e.State == StepState.ManuallyConfirmed);
                    break;
            }

            var stepList = filteredSteps.ToList();
            if (stepList.Count == 0)
            {
                var emptyCard = CloudSaveUIStyle.CreateCard("Nenhum item para exibir neste filtro", "Altere o filtro acima para visualizar outros passos.");
                emptyCard.style.paddingTop = 18;
                emptyCard.style.paddingBottom = 18;
                container.Add(emptyCard);
                return container;
            }

            foreach (var group in stepList
                         .GroupBy(e => e.Definition.Category)
                         .OrderBy(g => (int)g.Key))
            {
                var groupCard = CloudSaveUIStyle.CreateCard(GetCategoryDisplayName(group.Key));

                if (group.Key == StepCategory.Providers)
                {
                    var guideBox = new VisualElement();
                    guideBox.style.backgroundColor = new StyleColor(new Color(0.08f, 0.22f, 0.35f, 0.7f));
                    guideBox.style.borderLeftColor = new StyleColor(CloudSaveUIStyle.ColAccent);
                    guideBox.style.borderLeftWidth = 3.5f;
                    guideBox.style.SetRadius(4);
                    guideBox.style.paddingLeft = 10;
                    guideBox.style.paddingRight = 10;
                    guideBox.style.paddingTop = 8;
                    guideBox.style.paddingBottom = 8;
                    guideBox.style.marginBottom = 10;

                    var guideTitle = new Label("💡 Game Services vs Web Logins (Unity Dashboard Setup)");
                    guideTitle.style.fontSize = 11.5f;
                    guideTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
                    guideTitle.style.color = new StyleColor(CloudSaveUIStyle.ColAccent);
                    guideTitle.style.marginBottom = 4;
                    guideBox.Add(guideTitle);

                    var guideText = new Label(
                        "• Para jogos mobile, use os Game Services nativos: Google Play Games (Android) e Apple Game Center (iOS).\n" +
                        "• Vantagens: Login 100% silencioso/automático, sem telas de e-mail, e total sincronia com os Achievements.\n" +
                        "• ⚠️ No Unity Dashboard (Add Identity Provider):\n" +
                        "   - Escolha 'Google Play Games' (NÃO use 'Google' web).\n" +
                        "   - Escolha 'Apple Game Center' (NÃO use 'Sign in with Apple').");
                    guideText.style.fontSize = 10.5f;
                    guideText.style.color = new StyleColor(CloudSaveUIStyle.ColTextWhite);
                    guideText.style.whiteSpace = WhiteSpace.Normal;
                    guideBox.Add(guideText);

                    groupCard.Add(guideBox);
                }

                foreach (var eval in group)
                {
                    groupCard.Add(BuildStepCard(eval));
                }

                container.Add(groupCard);
            }

            return container;
        }

        private void AddFilterButton(VisualElement bar, string text, StepFilter filter, bool active, Color? activeAccent = null)
        {
            var btn = new Button(() =>
            {
                _stepFilter = filter;
                RenderActiveTab();
            })
            {
                text = text
            };
            btn.style.height = 26;
            btn.style.fontSize = 10.5f;
            btn.style.unityFontStyleAndWeight = FontStyle.Bold;
            btn.style.SetRadius(4);
            btn.style.marginRight = 6;
            btn.style.marginBottom = 4;
            btn.style.paddingLeft = 10;
            btn.style.paddingRight = 10;

            if (active)
            {
                btn.style.backgroundColor = new StyleColor(activeAccent ?? CloudSaveUIStyle.ColAccent);
                btn.style.color = new StyleColor(Color.white);
                btn.style.borderTopColor = new StyleColor(activeAccent ?? CloudSaveUIStyle.ColAccent);
                btn.style.borderBottomColor = new StyleColor(activeAccent ?? CloudSaveUIStyle.ColAccent);
                btn.style.borderLeftColor = new StyleColor(activeAccent ?? CloudSaveUIStyle.ColAccent);
                btn.style.borderRightColor = new StyleColor(activeAccent ?? CloudSaveUIStyle.ColAccent);
            }
            else
            {
                btn.style.backgroundColor = new StyleColor(new Color(0.14f, 0.16f, 0.22f));
                btn.style.color = new StyleColor(CloudSaveUIStyle.ColTextMuted);
                btn.style.borderTopColor = new StyleColor(CloudSaveUIStyle.ColCardBorder);
                btn.style.borderBottomColor = new StyleColor(CloudSaveUIStyle.ColCardBorder);
                btn.style.borderLeftColor = new StyleColor(CloudSaveUIStyle.ColCardBorder);
                btn.style.borderRightColor = new StyleColor(CloudSaveUIStyle.ColCardBorder);
            }

            bar.Add(btn);
        }

        private VisualElement BuildStepCard(StepEvaluation eval)
        {
            var isExpanded = _expandedSteps.Contains(eval.Definition.Id);
            var card = new VisualElement();
            card.AddToClassList("cs-step-card");
            card.style.backgroundColor = new StyleColor(new Color(0.14f, 0.16f, 0.22f));
            card.style.borderTopWidth = 1;
            card.style.borderBottomWidth = 1;
            card.style.borderRightWidth = 1;
            card.style.borderTopColor = new StyleColor(CloudSaveUIStyle.ColCardBorder);
            card.style.borderBottomColor = new StyleColor(CloudSaveUIStyle.ColCardBorder);
            card.style.borderRightColor = new StyleColor(CloudSaveUIStyle.ColCardBorder);
            card.style.SetRadius(6);
            card.style.paddingTop = 8;
            card.style.paddingBottom = 8;
            card.style.paddingLeft = 10;
            card.style.paddingRight = 10;
            card.style.marginBottom = 6;
            card.style.borderLeftWidth = 3.5f;

            var (badgeText, badgeClass) = eval.State switch
            {
                StepState.Validated => ("VALIDADO ✔", "cs-badge-pass"),
                StepState.ManuallyConfirmed => ("CONFIRMADO", "cs-badge-info"),
                StepState.NeedsValidation => ("REQUER TESTE", "cs-badge-warn"),
                StepState.NeedsAttention => ("ATENÇÃO ⚠️", "cs-badge-warn"),
                StepState.Failed => ("FALHOU ✗", "cs-badge-fail"),
                StepState.Blocked => ("BLOQUEADO ⛔", "cs-badge-fail"),
                _ => ("NÃO CONFIGURADO", "cs-badge-neutral")
            };

            var stateColor = eval.State switch
            {
                StepState.Validated => CloudSaveUIStyle.ColGreen,
                StepState.Failed => CloudSaveUIStyle.ColRed,
                StepState.Blocked => CloudSaveUIStyle.ColRed,
                _ => CloudSaveUIStyle.ColAmber
            };

            card.style.borderLeftColor = new StyleColor(stateColor);

            // Header Row
            var header = new VisualElement();
            header.AddToClassList("cs-step-header");
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.justifyContent = Justify.SpaceBetween;
            header.style.paddingTop = 2;
            header.style.paddingBottom = 2;

            var titleRow = new VisualElement();
            titleRow.style.flexDirection = FlexDirection.Row;
            titleRow.style.alignItems = Align.Center;

            var toggleIcon = new Label(isExpanded ? "▾" : "▸");
            toggleIcon.style.fontSize = 13;
            toggleIcon.style.color = new StyleColor(CloudSaveUIStyle.ColTextMuted);
            toggleIcon.style.marginRight = 6;
            toggleIcon.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleRow.Add(toggleIcon);

            var titleLbl = new Label(eval.Definition.Title);
            titleLbl.style.fontSize = 12;
            titleLbl.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleLbl.style.color = new StyleColor(CloudSaveUIStyle.ColTextWhite);
            titleRow.Add(titleLbl);

            var oblText = eval.Definition.Obligation switch
            {
                Obligation.Required => "Obrigatório",
                Obligation.Recommended => "Recomendado",
                _ => "Opcional"
            };
            var oblColor = eval.Definition.Obligation switch
            {
                Obligation.Required => CloudSaveUIStyle.ColRed,
                Obligation.Recommended => CloudSaveUIStyle.ColAccent,
                _ => CloudSaveUIStyle.ColTextMuted
            };

            var oblLbl = new Label($"({oblText})");
            oblLbl.style.fontSize = 10;
            oblLbl.style.color = new StyleColor(oblColor);
            oblLbl.style.marginLeft = 6;
            oblLbl.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleRow.Add(oblLbl);

            header.Add(titleRow);

            var badge = CloudSaveUIStyle.CreateBadge(badgeText, badgeClass);
            header.Add(badge);

            card.Add(header);

            // Body
            var body = new VisualElement();
            body.AddToClassList("cs-step-details");
            body.style.marginTop = 8;
            body.style.paddingTop = 8;
            body.style.borderTopWidth = 1;
            body.style.borderTopColor = new StyleColor(new Color(1f, 1f, 1f, 0.08f));

            // Blocked Banner if blocked
            if (eval.State == StepState.Blocked)
            {
                string blockedReason = null;
                string unblockStepId = eval.BlockedByStepId;

                if (!string.IsNullOrEmpty(unblockStepId) && _snapshot != null)
                {
                    var upstream = _snapshot.Steps.FirstOrDefault(s => s.Definition.Id == unblockStepId);
                    if (upstream != null)
                    {
                        blockedReason = $"Bloqueado por: \"{upstream.Definition.Title}\" ({upstream.State}). Conclua o passo anterior primeiro.";
                    }
                }

                if (string.IsNullOrEmpty(blockedReason) && eval.Definition.DependsOn != null && _snapshot != null)
                {
                    foreach (var dep in eval.Definition.DependsOn)
                    {
                        var upstream = _snapshot.Steps.FirstOrDefault(s => s.Definition.Id == dep.DependsOnId);
                        if (upstream != null && upstream.State != StepState.Validated && upstream.State != StepState.ManuallyConfirmed)
                        {
                            blockedReason = $"Bloqueado por: \"{upstream.Definition.Title}\" ({upstream.State}). Conclua o passo anterior primeiro.";
                            unblockStepId = upstream.Definition.Id;
                            break;
                        }
                    }
                }

                if (!string.IsNullOrEmpty(blockedReason))
                {
                    var blockBox = new VisualElement();
                    blockBox.style.backgroundColor = new StyleColor(new Color(0.94f, 0.27f, 0.27f, 0.15f));
                    blockBox.style.borderLeftColor = new StyleColor(CloudSaveUIStyle.ColRed);
                    blockBox.style.borderLeftWidth = 3;
                    blockBox.style.SetRadius(4);
                    blockBox.style.paddingLeft = 8;
                    blockBox.style.paddingRight = 8;
                    blockBox.style.paddingTop = 6;
                    blockBox.style.paddingBottom = 6;
                    blockBox.style.marginBottom = 8;
                    blockBox.style.flexDirection = FlexDirection.Row;
                    blockBox.style.alignItems = Align.Center;
                    blockBox.style.justifyContent = Justify.SpaceBetween;

                    var blockLbl = new Label($"⛔ {blockedReason}");
                    blockLbl.style.fontSize = 11;
                    blockLbl.style.color = new StyleColor(new Color(1f, 0.7f, 0.7f));
                    blockLbl.style.flexGrow = 1;
                    blockLbl.style.whiteSpace = WhiteSpace.Normal;
                    blockBox.Add(blockLbl);

                    if (!string.IsNullOrEmpty(unblockStepId))
                    {
                        var jumpBtn = CloudSaveUIStyle.CreateButton("🔍 Ir para Passo", "cs-btn-secondary", () =>
                        {
                            SwitchToChecklistAndExpand(unblockStepId);
                        });
                        jumpBtn.style.marginLeft = 6;
                        blockBox.Add(jumpBtn);
                    }

                    body.Add(blockBox);
                }
            }

            var c = eval.Definition.Copy;
            AddStepProp(body, "📌 O que é:", c.WhatIsThis);
            AddStepProp(body, "💡 Por que é necessário:", c.WhyNeeded);
            AddStepProp(body, "🛠️ O que fazer:", c.WhatYouDo);
            AddStepProp(body, "🧪 Como testar / validar:", !string.IsNullOrEmpty(c.HowToTest) ? c.HowToTest : c.WhatWeAutoVerify);
            AddStepProp(body, "🎯 Resultado esperado:", c.ExpectedResult);

            // Config found / missing
            if (eval.ConfigFound.Count > 0)
            {
                foreach (var f in eval.ConfigFound)
                {
                    var row = new Label($"✓ {f}");
                    row.style.fontSize = 10.5f;
                    row.style.color = new StyleColor(CloudSaveUIStyle.ColGreen);
                    row.style.marginLeft = 8;
                    body.Add(row);
                }
            }

            if (eval.ConfigMissing.Count > 0)
            {
                foreach (var m in eval.ConfigMissing)
                {
                    var row = new Label($"✗ {m}");
                    row.style.fontSize = 10.5f;
                    row.style.color = new StyleColor(CloudSaveUIStyle.ColAmber);
                    row.style.marginLeft = 8;
                    body.Add(row);
                }
            }

            // Validation result feedback banner
            if (eval.LastRecord != null && !string.IsNullOrEmpty(eval.LastRecord.Message))
            {
                var recordBox = new VisualElement();
                bool isPass = eval.LastRecord.Outcome == nameof(ValidationOutcome.Passed);
                recordBox.style.backgroundColor = new StyleColor(isPass ? new Color(0.10f, 0.73f, 0.51f, 0.12f) : new Color(0.94f, 0.27f, 0.27f, 0.15f));
                recordBox.style.borderLeftColor = new StyleColor(isPass ? CloudSaveUIStyle.ColGreen : CloudSaveUIStyle.ColRed);
                recordBox.style.borderLeftWidth = 3.5f;
                recordBox.style.SetRadius(4);
                recordBox.style.paddingLeft = 10;
                recordBox.style.paddingRight = 10;
                recordBox.style.paddingTop = 6;
                recordBox.style.paddingBottom = 6;
                recordBox.style.marginTop = 6;
                recordBox.style.marginBottom = 6;

                var recordTitle = new Label(isPass ? "✔ Último Teste Runtime (Sucesso):" : "✗ Último Teste Runtime (Falha):");
                recordTitle.style.fontSize = 11;
                recordTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
                recordTitle.style.color = new StyleColor(isPass ? CloudSaveUIStyle.ColGreen : CloudSaveUIStyle.ColRed);
                recordTitle.style.marginBottom = 2;
                recordBox.Add(recordTitle);

                var recordMsg = new Label(eval.LastRecord.Message);
                recordMsg.style.fontSize = 10.5f;
                recordMsg.style.color = new StyleColor(CloudSaveUIStyle.ColTextWhite);
                recordMsg.style.whiteSpace = WhiteSpace.Normal;
                recordBox.Add(recordMsg);

                if (!isPass && eval.LastRecord.Message.Contains("Play Mode"))
                {
                    var playHint = new Label("💡 Solução: O SDK da Unity Services exige o Play Mode para conectar à nuvem. Dê Play (▶️) no Unity e clique em 'Testar Novamente'.");
                    playHint.style.fontSize = 10.5f;
                    playHint.style.unityFontStyleAndWeight = FontStyle.Bold;
                    playHint.style.color = new StyleColor(CloudSaveUIStyle.ColAmber);
                    playHint.style.marginTop = 4;
                    playHint.style.whiteSpace = WhiteSpace.Normal;
                    recordBox.Add(playHint);
                }

                body.Add(recordBox);
            }

            // Action controls
            var actionToolbar = new VisualElement();
            actionToolbar.style.flexDirection = FlexDirection.Row;
            actionToolbar.style.alignItems = Align.Center;
            actionToolbar.style.marginTop = 8;

            if (eval.Definition.HasRuntimeValidator && eval.State != StepState.Blocked)
            {
                var runBtn = CloudSaveUIStyle.CreateButton(
                    eval.Runtime == RuntimeVerificationStatus.Passed ? "Testar Novamente" : "Testar Validação",
                    "cs-btn-primary",
                    () => RunValidation(eval)
                );
                runBtn.style.marginRight = 6;
                actionToolbar.Add(runBtn);
            }

            if (eval.Definition.Id == SetupRegistry.Ids.ComplianceDeletionUi)
            {
                var genBtn = CloudSaveUIStyle.CreateButton("📦 Gerar UI Prefab", "cs-btn-primary", () =>
                {
                    CloudSaveUIPrefabGenerator.GenerateAccountDeletionUI();
                    RecomputeDiagnostics();
                    RenderActiveTab();
                });
                genBtn.style.marginRight = 6;
                actionToolbar.Add(genBtn);

                var addSceneBtn = CloudSaveUIStyle.CreateButton("📌 Inserir na Cena", "cs-btn-secondary", () =>
                {
                    CloudSaveUIPrefabGenerator.CreateAccountDeletionUIInScene();
                });
                addSceneBtn.style.marginRight = 6;
                actionToolbar.Add(addSceneBtn);
            }

            var copyAiBtn = CloudSaveUIStyle.CreateButton("🤖 Copiar Prompt AI", "cs-btn-secondary", () =>
            {
                EditorGUIUtility.systemCopyBuffer = AiPromptFor(eval);
                Debug.Log($"[CloudSave] AI Prompt copied for {eval.Definition.Title}");
                EditorUtility.DisplayDialog("Cloud Save", "Prompt AI copiado para a área de transferência!", "OK");
            });
            copyAiBtn.style.marginRight = 6;
            actionToolbar.Add(copyAiBtn);

            if (c.Links != null)
            {
                foreach (var link in c.Links)
                {
                    var linkBtn = CloudSaveUIStyle.CreateButton(link.Label, "cs-btn-secondary", () =>
                    {
                        Application.OpenURL(link.Url);
                    });
                    linkBtn.style.marginRight = 4;
                    actionToolbar.Add(linkBtn);
                }
            }

            body.Add(actionToolbar);
            card.Add(body);

            void ApplyExpandedVisuals(bool expanded)
            {
                toggleIcon.text = expanded ? "▾" : "▸";
                toggleIcon.style.color = new StyleColor(expanded ? CloudSaveUIStyle.ColAccent : CloudSaveUIStyle.ColTextMuted);
                titleLbl.style.color = new StyleColor(expanded ? Color.white : new Color(0.90f, 0.92f, 0.96f));

                card.style.backgroundColor = new StyleColor(expanded ? new Color(0.18f, 0.22f, 0.30f) : new Color(0.14f, 0.16f, 0.22f));
                card.style.borderTopColor = new StyleColor(expanded ? new Color(0.35f, 0.44f, 0.60f, 0.65f) : CloudSaveUIStyle.ColCardBorder);
                card.style.borderBottomColor = new StyleColor(expanded ? new Color(0.35f, 0.44f, 0.60f, 0.65f) : CloudSaveUIStyle.ColCardBorder);
                card.style.borderRightColor = new StyleColor(expanded ? new Color(0.35f, 0.44f, 0.60f, 0.65f) : CloudSaveUIStyle.ColCardBorder);
                card.style.borderLeftColor = new StyleColor(expanded ? CloudSaveUIStyle.ColAccent : stateColor);
                card.style.borderLeftWidth = expanded ? 4.5f : 3.5f;

                body.style.display = expanded ? DisplayStyle.Flex : DisplayStyle.None;
            }

            ApplyExpandedVisuals(isExpanded);

            header.RegisterCallback<ClickEvent>(_ =>
            {
                isExpanded = !isExpanded;
                if (isExpanded) _expandedSteps.Add(eval.Definition.Id);
                else _expandedSteps.Remove(eval.Definition.Id);
                ApplyExpandedVisuals(isExpanded);
            });

            header.RegisterCallback<MouseEnterEvent>(_ =>
            {
                if (!isExpanded)
                    card.style.backgroundColor = new StyleColor(new Color(0.16f, 0.19f, 0.26f));
            });
            header.RegisterCallback<MouseLeaveEvent>(_ =>
            {
                if (!isExpanded)
                    card.style.backgroundColor = new StyleColor(new Color(0.14f, 0.16f, 0.22f));
            });

            return card;
        }

        private static string GetCategoryDisplayName(StepCategory cat) => cat switch
        {
            StepCategory.Prerequisites => "1. Conexão Unity Gaming Services (UGS)",
            StepCategory.Services => "2. Pacotes & Serviços UGS",
            StepCategory.StartupCode => "3. Código de Inicialização & Sincronia",
            StepCategory.SaveData => "4. Estrutura dos Dados de Salvar",
            StepCategory.Ui => "5. Interfaces de Usuário Prontas (Prefabs)",
            StepCategory.Providers => "6. Provedores Mobile (Google Play & Game Center)",
            StepCategory.StoreCompliance => "7. Conformidade das Lojas (Apple 5.1.1v & Google Play)",
            StepCategory.Migration => "8. Migração de Dados Legados",
            StepCategory.Verification => "9. Testes de Validação em Tempo Real",
            _ => cat.ToString()
        };

        private static (string statusText, string badgeClass, Color color) ComputePillarStatus(IEnumerable<StepEvaluation> evals)
        {
            var list = evals.Where(e => e.State != StepState.NotApplicable).ToList();
            if (list.Count == 0)
                return ("N/A", "cs-badge-neutral", CloudSaveUIStyle.ColTextMuted);

            bool hasFailed = list.Any(e => e.State == StepState.Failed);
            bool hasNotConfigured = list.Any(e => e.Definition.Obligation == Obligation.Required && (e.State == StepState.NotConfigured || e.State == StepState.Blocked));
            bool hasPending = list.Any(e => e.State == StepState.NeedsAttention || e.State == StepState.NeedsValidation);

            if (hasFailed || hasNotConfigured)
                return ("PENDENTE ⚠️", "cs-badge-fail", CloudSaveUIStyle.ColRed);
            if (hasPending)
                return ("ATENÇÃO ⚠️", "cs-badge-warn", CloudSaveUIStyle.ColAmber);

            return ("PRONTO ✔", "cs-badge-pass", CloudSaveUIStyle.ColGreen);
        }

        private bool IsStepReady(string stepId)
        {
            if (_snapshot == null) return false;
            var s = _snapshot.Steps.FirstOrDefault(x => x.Definition.Id == stepId);
            if (s == null) return false;
            return s.State == StepState.Validated ||
                   s.State == StepState.ManuallyConfirmed ||
                   s.Configuration == ConfigurationStatus.Present;
        }

        private void SwitchToChecklistAndExpand(string stepId)
        {
            _diagViewMode = DiagnosticsViewMode.FullChecklist;
            _stepFilter = StepFilter.All;
            _expandedSteps.Clear();
            if (!string.IsNullOrEmpty(stepId))
            {
                _expandedSteps.Add(stepId);
            }
            RenderActiveTab();
        }

        private static void CopyControllerSnippet()
        {
            const string snippet = @"// Exemplo de Inicialização com CloudSaveController (Recomendado)
using Wagenheimer.CloudSave;
using UnityEngine;

public class MyGameSaveManager : MonoBehaviour
{
    private CloudSaveController _cloudSave;

    private async void Start()
    {
        _cloudSave = CloudSaveController.Create(new CloudSaveOptions
        {
            SaveKey = ""GameSaveSlot"",
            Serialize = () => System.Text.Encoding.UTF8.GetBytes(JsonUtility.ToJson(mySaveData)),
            Deserialize = bytes =>
            {
                var json = System.Text.Encoding.UTF8.GetString(bytes);
                mySaveData = JsonUtility.FromJson<MySaveData>(json);
                SaveToLocalDisk();
            },
            OnCloudApplied = () => RefreshUI()
        });

        await _cloudSave.StartAsync();
    }

    public void OnLocalSaveChanged()
    {
        SaveToLocalDisk();
        _cloudSave?.MarkDirty();
    }

    private void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus) _ = _cloudSave?.FlushAsync();
    }

    private void OnApplicationQuit()
    {
        _ = _cloudSave?.FlushAsync();
    }
}";
            EditorGUIUtility.systemCopyBuffer = snippet;
            EditorUtility.DisplayDialog("Cloud Save", "Código de integração (CloudSaveController) copiado para a área de transferência!", "OK");
        }

        private static void AddStepProp(VisualElement container, string label, string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            var row = new VisualElement();
            row.style.marginBottom = 6;
            row.style.paddingLeft = 8;
            row.style.paddingRight = 8;
            row.style.paddingTop = 4;
            row.style.paddingBottom = 4;
            row.style.backgroundColor = new StyleColor(new Color(0f, 0f, 0f, 0.15f));
            row.style.SetRadius(4);

            var lbl = new Label(label);
            lbl.style.fontSize = 11;
            lbl.style.unityFontStyleAndWeight = FontStyle.Bold;
            lbl.style.color = new StyleColor(new Color(0.40f, 0.70f, 1f)); // Soft blue highlight for prop labels
            lbl.style.marginBottom = 2;

            var val = new Label(text);
            val.style.fontSize = 11;
            val.style.color = new StyleColor(new Color(0.92f, 0.94f, 0.98f));
            val.style.whiteSpace = WhiteSpace.Normal;

            row.Add(lbl);
            row.Add(val);
            container.Add(row);
        }

        private void RecomputeDiagnostics()
        {
            _state = CloudSaveSetupState.GetOrCreate();
            _ctx = SetupContext.ForCurrentProject(_state.CustomUis);
            _snapshot = SetupModel.Compute(_registry, _ctx, _state);
        }

        private async void RunValidation(StepEvaluation e)
        {
            var testCase = _registry.CreateCaseFor(e.Definition.Id);
            if (testCase == null) return;

            if (!Application.isPlaying)
            {
                bool startPlay = EditorUtility.DisplayDialog(
                    "Play Mode Necessário para Teste",
                    "O SDK da Unity Services (UGS) só permite conectar aos servidores e testar os provedores durante o Play Mode.\n\n" +
                    "Deseja iniciar o Play Mode agora para executar a validação?",
                    "Sim, dar Play ▶️",
                    "Cancelar"
                );
                if (startPlay)
                {
                    EditorApplication.isPlaying = true;
                }
                return;
            }

            _isBusy = true;
            RenderActiveTab();

            ValidationResult result;
            try
            {
                result = await CloudSaveVerifier.RunAsync(testCase);
            }
            catch (Exception ex)
            {
                result = ValidationResult.Fail(testCase.Id, "Unexpected error.", ex);
            }

            _state.RecordValidation(new ValidationRecord
            {
                StepId = e.Definition.Id,
                CaseId = result.CaseId,
                Outcome = result.Outcome.ToString(),
                Fingerprint = e.CurrentFingerprint,
                StartedAtUtc = result.StartedAtUtc.ToString("o"),
                DurationMs = result.DurationMs,
                PackageVersion = _version,
                UgsEnvironment = "",
                Message = result.Message,
            });

            _isBusy = false;
            RecomputeDiagnostics();
            RenderActiveTab();
        }

        private static string AiPromptFor(StepEvaluation e)
        {
            if (!string.IsNullOrEmpty(e.Definition.AiPrompt))
                return e.Definition.AiPrompt;

            var c = e.Definition.Copy;
            return $"In this Unity project, complete this Cloud Save setup step: \"{e.Definition.Title}\".\n" +
                   $"What it is: {c.WhatIsThis}\n" +
                   $"What to do: {c.WhatYouDo}\n" +
                   $"Verify by: {c.HowToTest}\n" +
                   $"Expected result: {c.ExpectedResult}\n" +
                   "Make the minimal change, matching the project's existing conventions.";
        }

        private void CopyDiagnosticPrompt()
        {
            if (_snapshot == null) return;
            var issues = _snapshot.Steps.Where(s => s.State == StepState.Failed || s.State == StepState.NeedsAttention || s.State == StepState.Blocked).ToList();
            if (issues.Count == 0)
            {
                EditorUtility.DisplayDialog("Cloud Save", "All integration steps are validated and ready!", "OK");
                return;
            }

            var lines = new List<string> { "Please fix the following issues in the Unity CloudSave setup:" };
            int i = 1;
            foreach (var step in issues)
            {
                lines.Add($"\n{i++}. {step.Definition.Title} ({step.State})");
                lines.Add($"   Details: {step.Definition.Copy.WhatIsThis}");
                lines.Add($"   Fix: {step.Definition.Copy.WhatYouDo}");
            }

            EditorGUIUtility.systemCopyBuffer = string.Join("\n", lines);
            EditorUtility.DisplayDialog("Cloud Save", "Issue report copied to clipboard!", "OK");
        }

        #endregion

        #region Tab 1: Live QA & Cloud Tester (HELPER)

        private VisualElement BuildLiveQAView()
        {
            var scroll = new ScrollView();
            scroll.style.flexGrow = 1;

            // 1. Live Runtime Status Card
            var liveCard = CloudSaveUIStyle.CreateCard("Live Authentication & Cloud State");

            AddStateRow(liveCard, "Play Mode Active", Application.isPlaying ? "YES (Active)" : "NO (Edit Mode)");
            AddStateRow(liveCard, "UGS Initialized & Ready", CloudAuth.IsReady ? "YES" : "NO");
            AddStateRow(liveCard, "Authenticated", CloudAuth.IsSignedIn ? $"YES ({CloudAuth.PlayerId})" : "NO");
            AddStateRow(liveCard, "Sync Engine Status", CloudSync.LastResult.HasValue ? CloudSync.LastResult.Value.ToString() : "No sync yet");
            AddStateRow(liveCard, "Active Provider", CloudAuth.Provider.ToString());

            scroll.Add(liveCard);

            // 2. Interactive Cloud Key/Value Tester
            var testCard = CloudSaveUIStyle.CreateCard("Interactive Cloud Storage Tester", "Save, load, and test key-value payloads against Unity Cloud Save.");

            var keyRow = new VisualElement();
            keyRow.style.flexDirection = FlexDirection.Row;
            keyRow.style.alignItems = Align.Center;
            keyRow.style.marginBottom = 6;

            var keyLbl = new Label("Save Key:");
            keyLbl.style.width = 110;
            keyLbl.style.fontSize = 11;
            keyLbl.style.unityFontStyleAndWeight = FontStyle.Bold;
            keyRow.Add(keyLbl);

            var keyField = new TextField { value = _testKey };
            keyField.style.flexGrow = 1;
            keyField.RegisterValueChangedCallback(e => _testKey = e.newValue);
            keyRow.Add(keyField);
            testCard.Add(keyRow);

            var valRow = new VisualElement();
            valRow.style.flexDirection = FlexDirection.Row;
            valRow.style.alignItems = Align.Center;
            valRow.style.marginBottom = 8;

            var valLbl = new Label("Payload / Data:");
            valLbl.style.width = 110;
            valLbl.style.fontSize = 11;
            valLbl.style.unityFontStyleAndWeight = FontStyle.Bold;
            valRow.Add(valLbl);

            var valField = new TextField { value = _testValue };
            valField.style.flexGrow = 1;
            valField.RegisterValueChangedCallback(e => _testValue = e.newValue);
            valRow.Add(valField);
            testCard.Add(valRow);

            var testerToolbar = new VisualElement();
            testerToolbar.style.flexDirection = FlexDirection.Row;
            testerToolbar.style.flexWrap = Wrap.Wrap;

            testerToolbar.Add(CloudSaveUIStyle.CreateButton("💾 Save Key to Cloud", "cs-btn-primary", async () =>
            {
                if (!Application.isPlaying)
                {
                    EditorUtility.DisplayDialog("Cloud Save", "Enter Play Mode to test runtime CloudSave API calls.", "OK");
                    return;
                }
                _testConsoleLog = $"[{DateTime.Now:HH:mm:ss}] Saving {_testKey}...";
                RenderActiveTab();
                try
                {
                    var bytes = System.Text.Encoding.UTF8.GetBytes(_testValue);
                    await CloudSync.SaveAsync(bytes, DateTimeOffset.UtcNow.ToUnixTimeSeconds());
                    _testConsoleLog += $"\n✔ Saved {_testKey} successfully!";
                }
                catch (Exception ex)
                {
                    _testConsoleLog += $"\n✗ Save failed: {ex.Message}";
                }
                RenderActiveTab();
            }));

            testerToolbar.Add(CloudSaveUIStyle.CreateButton("📥 Load Key from Cloud", "cs-btn-secondary", async () =>
            {
                if (!Application.isPlaying)
                {
                    EditorUtility.DisplayDialog("Cloud Save", "Enter Play Mode to test runtime CloudSave API calls.", "OK");
                    return;
                }
                _testConsoleLog = $"[{DateTime.Now:HH:mm:ss}] Loading {_testKey}...";
                RenderActiveTab();
                try
                {
                    var (bytes, ts) = await CloudSync.LoadRawCloudDataAsync();
                    var text = bytes != null ? System.Text.Encoding.UTF8.GetString(bytes) : "(empty)";
                    _testConsoleLog += $"\n✔ Loaded: {text} (timestamp: {ts})";
                }
                catch (Exception ex)
                {
                    _testConsoleLog += $"\n✗ Load failed: {ex.Message}";
                }
                RenderActiveTab();
            }));

            testerToolbar.Add(CloudSaveUIStyle.CreateButton("🗑️ Delete Key", "cs-btn-secondary", async () =>
            {
                if (!Application.isPlaying) return;
                try
                {
                    await CloudSync.DeleteCloudSaveAsync();
                    _testConsoleLog = $"✔ Deleted {_testKey} from cloud.";
                }
                catch (Exception ex)
                {
                    _testConsoleLog += $"\n✗ Delete failed: {ex.Message}";
                }
                RenderActiveTab();
            }));

            testerToolbar.Add(CloudSaveUIStyle.CreateButton("🔄 Force Full Sync", "cs-btn-secondary", async () =>
            {
                if (!Application.isPlaying) return;
                try
                {
                    await CloudSync.InitAndSyncAsync(DateTimeOffset.UtcNow.ToUnixTimeSeconds(), b =>
                    {
                        _testConsoleLog = $"✔ Full sync resolved cloud data ({b?.Length ?? 0} bytes).";
                    });
                }
                catch (Exception ex)
                {
                    _testConsoleLog += $"✗ Sync error: {ex.Message}";
                }
                RenderActiveTab();
            }));

            testCard.Add(testerToolbar);

            if (!string.IsNullOrEmpty(_testConsoleLog))
            {
                var consoleBox = new VisualElement();
                consoleBox.AddToClassList("cs-code-box");
                var logLbl = new Label(_testConsoleLog);
                logLbl.style.fontSize = 11;
                logLbl.style.color = new StyleColor(new Color(0.43f, 0.91f, 0.72f));
                logLbl.style.whiteSpace = WhiteSpace.Normal;
                consoleBox.Add(logLbl);
                testCard.Add(consoleBox);
            }

            scroll.Add(testCard);

            // 3. UI Prefabs Generator Card
            var prefabCard = CloudSaveUIStyle.CreateCard("UI Prefabs & Drop-in Components", "Generate ready-made uGUI Canvas prefabs for your game.");

            bool hasCloudUI = File.Exists("Assets/Resources/CloudSaveUI.prefab") || File.Exists("Packages/com.wagenheimer.cloudsave/Runtime/Resources/CloudSaveUI.prefab");
            bool hasAuthUI = File.Exists("Assets/Resources/CloudAuthUI.prefab") || File.Exists("Packages/com.wagenheimer.cloudsave/Runtime/Resources/CloudAuthUI.prefab");
            bool hasSyncUI = File.Exists("Assets/Resources/SyncStatusUI.prefab") || File.Exists("Packages/com.wagenheimer.cloudsave/Runtime/Resources/SyncStatusUI.prefab");

            AddStateRow(prefabCard, "CloudSaveUI.prefab", hasCloudUI ? "FOUND (Ready)" : "MISSING");
            AddStateRow(prefabCard, "CloudAuthUI.prefab", hasAuthUI ? "FOUND (Ready)" : "MISSING");
            AddStateRow(prefabCard, "SyncStatusUI.prefab", hasSyncUI ? "FOUND (Ready)" : "MISSING");

            var prefToolbar = new VisualElement();
            prefToolbar.style.flexDirection = FlexDirection.Row;
            prefToolbar.style.marginTop = 8;

            prefToolbar.Add(CloudSaveUIStyle.CreateButton("📦 Generate All UI Prefabs", "cs-btn-primary", () =>
            {
                CloudSaveUIPrefabGenerator.GenerateAll();
                RenderActiveTab();
            }));

            prefToolbar.Add(CloudSaveUIStyle.CreateButton("🗑️ Generate Account Deletion UI", "cs-btn-secondary", () =>
            {
                CloudSaveUIPrefabGenerator.GenerateAccountDeletionUI();
                RenderActiveTab();
            }));

            prefToolbar.Add(CloudSaveUIStyle.CreateButton("🛠️ Add In-Game Debug Overlay to Scene", "cs-btn-secondary", () =>
            {
                CloudSaveDebugOverlayEditor.AddDebugOverlayToScene();
            }));

            prefabCard.Add(prefToolbar);
            scroll.Add(prefabCard);

            return scroll;
        }

        private static void AddStateRow(VisualElement card, string label, string value)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.justifyContent = Justify.SpaceBetween;
            row.style.paddingTop = 3;
            row.style.paddingBottom = 3;

            var lbl = new Label(label);
            lbl.style.color = new StyleColor(CloudSaveUIStyle.ColTextMuted);
            lbl.style.fontSize = 11;

            var val = new Label(value);
            val.style.fontSize = 11;
            val.style.unityFontStyleAndWeight = FontStyle.Bold;
            val.style.color = new StyleColor(CloudSaveUIStyle.ColTextWhite);

            row.Add(lbl);
            row.Add(val);
            card.Add(row);
        }

        #endregion

        #region Tab 2: Integration Guide & Code Reference

        private VisualElement BuildGuideView()
        {
            var scroll = new ScrollView();
            scroll.style.flexGrow = 1;

            var quickStartCard = CloudSaveUIStyle.CreateCard("Cloud Save Quick Start Guide", "Four simple steps to cross-platform cloud persistence.");

            AddGuideStep(quickStartCard, "1. Link Unity Services (UGS)",
                "Open Project Settings > Services and link your project with a Unity Gaming Services Organization. Enable Cloud Save in your Unity Cloud Dashboard.");

            AddGuideStep(quickStartCard, "2. Authenticate the Player",
                "Sign in anonymously on boot, or upgrade seamlessly to Google Play Games, Apple Game Center, or Steam: CloudAuth.SignInAnonymouslyAsync()");

            AddGuideStep(quickStartCard, "3. Save & Load Game Data",
                "Call CloudSaveController.SaveAsync(\"Slot1\", bytes) when saving game progress. On boot, call CloudSaveController.LoadAsync<byte[]>(\"Slot1\").");

            AddGuideStep(quickStartCard, "4. Add Sync Status UI",
                "Drop 'SyncStatusUI' into your HUD or Settings panel. It automatically displays a spinner while uploading and a checkmark when synced!");

            scroll.Add(quickStartCard);

            // Code Snippets Card
            var codeCard = CloudSaveUIStyle.CreateCard("Runtime API Code Snippets");

            AddCodeSnippet(codeCard, "1. Recommended: CloudSaveController Lifecycle",
                "// 1. Initialize once at game startup:\n" +
                "var cloud = CloudSaveController.Create(new CloudSaveOptions\n" +
                "{\n" +
                "    SaveKey = \"Slot1\",\n" +
                "    Serialize = () => System.Text.Encoding.UTF8.GetBytes(JsonUtility.ToJson(mySaveData)),\n" +
                "    Deserialize = bytes => { mySaveData = JsonUtility.FromJson<SaveData>(System.Text.Encoding.UTF8.GetString(bytes)); LocalSaveToDisk(); },\n" +
                "    OnCloudApplied = () => RefreshUI()\n" +
                "});\n" +
                "await cloud.StartAsync();\n\n" +
                "// 2. Notify after any local save:\n" +
                "cloud.MarkDirty();\n\n" +
                "// 3. Flush on pause / quit:\n" +
                "private void OnApplicationPause(bool pause) { if (pause) _ = cloud.FlushAsync(); }");

            AddCodeSnippet(codeCard, "2. Store Compliance: Account Deletion (Apple 5.1.1(v) & Google Play)",
                "// Apple and Google Play REQUIRE an in-app account deletion button when auth/cloud is offered.\n" +
                "// When the player confirms deletion in UI:\n" +
                "bool success = await Wagenheimer.CloudSave.CloudAuth.DeleteAccountAsync();\n" +
                "if (success)\n" +
                "{\n" +
                "    // Crucial: Clear local save data / profiles from disk so the player starts fresh\n" +
                "    LocalSaveSystem.ClearAllProfiles();\n" +
                "    // Return to main menu or character creation\n" +
                "    SceneManager.LoadScene(\"MainMenu\");\n" +
                "}");

            AddCodeSnippet(codeCard, "3. Mobile Game Services Linking (Google Play Games & Game Center)",
                "// Upgrade anonymous player to native Google Play Games or Apple Game Center:\n" +
                "// Note: In Unity Cloud Dashboard, pick 'Google Play Games' or 'Apple Game Center' (NOT Web OAuth).\n" +
                "#if UNITY_ANDROID\n" +
                "await Wagenheimer.CloudSave.CloudAuth.LinkGooglePlayGamesAsync(serverAuthCode);\n" +
                "#elif UNITY_IOS\n" +
                "await Wagenheimer.CloudSave.CloudAuth.LinkAppleGameCenterAsync(signature, salt, timestamp, publicKeyUrl);\n" +
                "#endif");

            AddCodeSnippet(codeCard, "4. Manual Low-Level Sync (CloudSync API)",
                "byte[] bytes = System.Text.Encoding.UTF8.GetBytes(json);\n" +
                "await Wagenheimer.CloudSave.CloudSync.SaveAsync(bytes, DateTimeOffset.UtcNow.ToUnixTimeSeconds());\n\n" +
                "var (cloudBytes, ts) = await Wagenheimer.CloudSave.CloudSync.LoadRawCloudDataAsync();\n" +
                "if (cloudBytes != null) string json = System.Text.Encoding.UTF8.GetString(cloudBytes);");

            scroll.Add(codeCard);

            return scroll;
        }

        private static void AddGuideStep(VisualElement container, string title, string description)
        {
            var item = new VisualElement();
            item.style.marginBottom = 10;

            var t = new Label(title);
            t.style.fontSize = 12;
            t.style.unityFontStyleAndWeight = FontStyle.Bold;
            t.style.color = new StyleColor(CloudSaveUIStyle.ColTextWhite);

            var d = new Label(description);
            d.style.fontSize = 11;
            d.style.color = new StyleColor(CloudSaveUIStyle.ColTextMuted);
            d.style.whiteSpace = WhiteSpace.Normal;
            d.style.marginLeft = 6;
            d.style.marginTop = 2;

            item.Add(t);
            item.Add(d);
            container.Add(item);
        }

        private static void AddCodeSnippet(VisualElement container, string title, string code)
        {
            var item = new VisualElement();
            item.style.marginBottom = 10;

            var t = new Label(title);
            t.style.fontSize = 11.5f;
            t.style.unityFontStyleAndWeight = FontStyle.Bold;
            t.style.color = new StyleColor(CloudSaveUIStyle.ColTextWhite);
            item.Add(t);

            var codeBox = new VisualElement();
            codeBox.AddToClassList("cs-code-box");
            codeBox.style.backgroundColor = new StyleColor(new Color(0.08f, 0.09f, 0.12f));
            codeBox.style.SetRadius(6);
            codeBox.style.paddingTop = 8;
            codeBox.style.paddingBottom = 8;
            codeBox.style.paddingLeft = 10;
            codeBox.style.paddingRight = 10;
            codeBox.style.marginTop = 4;

            var codeLbl = new Label(code);
            codeLbl.style.fontSize = 10.5f;
            codeLbl.style.color = new StyleColor(new Color(0.43f, 0.91f, 0.72f));
            codeBox.Add(codeLbl);

            var copyBtn = CloudSaveUIStyle.CreateButton("Copy Snippet", "cs-btn-secondary", () =>
            {
                EditorGUIUtility.systemCopyBuffer = code;
                EditorUtility.DisplayDialog("Cloud Save", "Code snippet copied to clipboard!", "OK");
            });
            copyBtn.style.marginTop = 6;
            copyBtn.style.alignSelf = Align.FlexEnd;
            codeBox.Add(copyBtn);

            item.Add(codeBox);
            container.Add(item);
        }

        #endregion

        #region Tab 3: About & Updates

        private VisualElement BuildAboutView()
        {
            var scroll = new ScrollView();
            scroll.style.flexGrow = 1;

            var infoCard = CloudSaveUIStyle.CreateCard("Package Information", "Production-grade cross-platform cloud save system by Cezar Wagenheimer.");
            AddStateRow(infoCard, "Package Name", "com.wagenheimer.cloudsave");
            AddStateRow(infoCard, "Installed Version", _version);
            AddStateRow(infoCard, "Author", "Cezar Wagenheimer");
            AddStateRow(infoCard, "License", "MIT");

            var infoToolbar = new VisualElement();
            infoToolbar.style.flexDirection = FlexDirection.Row;
            infoToolbar.style.marginTop = 10;

            infoToolbar.Add(CloudSaveUIStyle.CreateButton("Check for Updates", "cs-btn-primary", () =>
            {
                PackageHubWindow.OpenToPackage("com.wagenheimer.cloudsave");
            }));

            infoToolbar.Add(CloudSaveUIStyle.CreateButton("GitHub Repository", "cs-btn-secondary", () =>
            {
                Application.OpenURL(RepoUrl);
            }));

            infoToolbar.Add(CloudSaveUIStyle.CreateButton("Report Issue", "cs-btn-secondary", () =>
            {
                Application.OpenURL(IssuesUrl);
            }));

            infoCard.Add(infoToolbar);
            scroll.Add(infoCard);

            // Capabilities Card
            var capCard = CloudSaveUIStyle.CreateCard("Key Capabilities");
            AddStateRow(capCard, "Unity Cloud Save (UGS)", "Server-authoritative JSON/byte data storage with optimistic concurrency");
            AddStateRow(capCard, "Multi-Provider Auth", "Anonymous, Google Play Games (Android), Game Center (iOS), Steam");
            AddStateRow(capCard, "Timestamp Conflict Resolution", "Deterministic conflict merging with offline caching");
            AddStateRow(capCard, "Drop-in UI Prefabs", "Pre-styled Canvas components for cloud sync indicators and account linking");
            scroll.Add(capCard);

            return scroll;
        }

        #endregion

        private void LoadPackageVersion()
        {
            try
            {
                if (File.Exists(PackageJsonPath))
                {
                    var json = File.ReadAllText(PackageJsonPath);
                    var match = Regex.Match(json, "\"version\"\\s*:\\s*\"([^\"]+)\"");
                    if (match.Success)
                    {
                        _version = match.Groups[1].Value;
                        return;
                    }
                }
            }
            catch { }
            _version = "4.26.0";
        }
    }
}
