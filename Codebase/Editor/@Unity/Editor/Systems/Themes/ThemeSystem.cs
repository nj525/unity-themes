using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityObject = UnityEngine.Object;
namespace Zios.Unity.Editor.Themes{
	using Zios.Events;
	using Zios.Extensions;
	using Zios.Extensions.Convert;
	using Zios.File;
	using Zios.Reflection;
	using Zios.Supports.Worker;
	using Zios.SystemAttributes;
	using Zios.Unity.Call;
	using Zios.Unity.Editor.Extensions;
	using Zios.Unity.Editor.Locate;
	using Zios.Unity.Editor.Pref;
	using Zios.Unity.ProxyEditor;
	using Zios.Unity.EditorUI;
	using Zios.Unity.Editor.Undo;
	using Zios.Unity.Extensions;
	using Zios.Unity.Locate;
	using Zios.Unity.Log;
	using Zios.Unity.Proxy;
	using Zios.Unity.Supports.Singleton;
	using Zios.Unity.Time;
	using Zios.Shortcuts;
	using Zios.Unity.Shortcuts;
	//asm Zios.Shortcuts;
	//asm Zios.Unity.Supports.Singleton;
	//asm Zios.Unity.Shortcuts;
	using Editor = UnityEditor.Editor;
	public enum HoverResponse{None=1,Slow,Moderate,Instant};
	[InitializeOnLoad][NotSerialized]
	public partial class Theme{
		public static string revision = "[r{revision}]";
		public static string storagePath;
		public static string[] buildTerms = new string[]{"Styles","styles","s_GOStyles","s_Current","s_Styles","m_Styles","ms_Styles","constants","s_Defaults","ms_Loaded","ms_LoadedIcons"};
		public static int themeIndex;
		public static int paletteIndex;
		public static int fontsetIndex;
		public static int iconsetIndex;
		public static int skinsetIndex;
		public static HoverResponse hoverResponse = HoverResponse.None;
		public static bool separatePlaymode;
		public static bool showColorsAdvanced;
		public static bool showFontsAdvanced;
		public static bool initialized;
		public static bool lazyLoaded;
		public static bool disabled;
		public static bool debug;
		public static ThemeWindow window;
		public static string suffix;
		public static bool needsSetup = true;
		private static bool needsIconUpdate = true;
		private static bool needsImageUpdate = true;
		private static bool needsVariantReset;
		private static bool needsReset;
		private static bool needsRefresh;
		private static bool needsRebuild;
		private static bool setupPreferences;
		private static bool liveEdit;
		private static Vector2 preferenceScroll = Vector2.zero;
		private static float colorChangeTime;
		private static int colorChangeCount;
		private static Action undoCallback;
		private static List<string> paletteNames = new List<string>();
		private static List<string> fontsetNames = new List<string>();
		private static List<string> fontNames = new List<string>();
		private static Font[] fonts = new Font[0];
		private static Font[] builtinFonts = new Font[0];
		static Theme(){
			ProxyEditor.AddModeChange(Theme.CheckUpdate);
			ProxyEditor.AddUpdate(ThemeWindow.ShowWindow);
			Events.Add("On GUISkin Changed",()=>{
				if(Theme.liveEdit){
					Call.Delay(Theme.Rebuild,0.5f);
				}
			});
			//Theme.needsSetup = !ProxyEditor.IsPlaying();
		}
		public static void Update(){
			if(Theme.disabled){return;}
			if(Theme.needsReset){Theme.InstantReset();}
			else if(Theme.needsRebuild){
				Time.Start();
				Theme.ClearStyles();
				Theme.ApplyIconset(Theme.needsIconUpdate);
				EditorPref.Call("EditorTheme-Rebuild",Theme.debug);
				EditorPref.Call("Zios.Theme.Rebuild",Theme.debug);
				Theme.Refresh();
				Theme.needsRebuild = false;
				Theme.needsIconUpdate = false;
				if(Theme.debug){Log.Show("Rebuild","[Themes-Debug] Rebuild -- " + Time.Passed());}
			}
			else if(Theme.needsRefresh){
				Theme.ApplySettings();
				EditorPref.Call("EditorTheme-Refresh",Theme.debug);
				EditorPref.Call("Zios.Theme.Refresh",Theme.debug);
				//Theme.Cleanup();
				ProxyEditor.RepaintAll();
				Theme.needsRefresh = false;
			}
			else if(Theme.needsSetup){
                Time.Start();
				var root = File.Find("Default.unitytheme",Theme.debug);
				if(root.IsNull()){
					Log.Warning("[Themes] No .unityTheme files found. Disabling until refreshed.");
					Theme.disabled = EditorPref.Set<bool>("Zios.Theme.Options.Disabled",true);
					Theme.needsSetup = false;
					return;
				}
				Theme.storagePath = root.path.GetDirectory()+"/";
				Theme.Load(!Theme.initialized);
				EditorPref.Call("EditorTheme-Setup",Theme.debug);
				EditorPref.Call("Zios.Theme.Setup",Theme.debug);
				Theme.setupPreferences = false;
				Theme.needsSetup = false;
				Theme.initialized = true;
				PlayerSettings.scriptingRuntimeVersion = ScriptingRuntimeVersion.Latest;
                PlayerSettings.SetApiCompatibilityLevel(BuildPipeline.GetBuildTargetGroup(EditorUserBuildSettings.activeBuildTarget), ApiCompatibilityLevel.NET_4_6);
				if(Theme.debug){Log.Show("[Themes-Debug] Setup -- " + Time.Passed());}
				Theme.Rebuild();
				Theme.Update();
			}
		}
		public static void LoadCheck(){
			if(Theme.lazyLoaded){
				Theme.paletteNames.Clear();
				Theme.fontsetNames.Clear();
				Theme.Load(false);
				Theme.ApplySettings();
			}
		}
		public static void Load(bool lazy=false){
			File.monitor = false;
			var themeName = EditorPref.Build("Zios.Theme","Default");
			RelativeColor.autoBalance = EditorPref.Build("Zios.Theme.Palette.Autobalance",1).As<AutoBalance>();
			Theme.showColorsAdvanced = EditorPref.Build("Zios.Theme.Palette.Advanced",false);
			Theme.showFontsAdvanced = EditorPref.Build("Zios.Theme.Fontset.Advanced",false);
			Theme.hoverResponse = EditorPref.Build("Zios.Theme.Options.HoverResponse",1).As<HoverResponse>();
			Theme.separatePlaymode = EditorPref.Build("Zios.Theme.Options.SeparatePlaymode",false);
			Theme.suffix = ProxyEditor.IsChanging() && Theme.separatePlaymode ? ".Playmode" : "";
			Theme.suffix = "."+themeName+Theme.suffix;
			var fontset = lazy ? EditorPref.Get<string>("Zios.Theme.Fontset"+Theme.suffix,"Classic") + ".unityFontset" : null;
			var palette = lazy ? EditorPref.Get<string>("Zios.Theme.Palette"+Theme.suffix,"Classic") + ".unityPalette" : null;
			var iconset = lazy ? File.Find("Iconsets/"+EditorPref.Get<string>("Zios.Theme.Iconset"+Theme.suffix,"Default")) : null;
			var skinset = lazy ? File.Find("Skinsets/"+EditorPref.Get<string>("Zios.Theme.Skinset"+Theme.suffix,"Default")) : null;
			var unityTheme = lazy ? themeName + ".unitytheme" : null;
			ThemeFontset.all = ThemeFontset.Import(fontset);
			ThemePalette.all = ThemePalette.Import(palette);
			ThemeSkinset.all = skinset.IsNull() ? ThemeSkinset.Import() : ThemeSkinset.Import(skinset.path).AsList();
			ThemeIconset.all = iconset.IsNull() ? ThemeIconset.Import() : ThemeIconset.Import(iconset.path).AsList();
			Theme.all = Theme.Import(unityTheme).OrderBy(x=>x.name!="Default").ToList();
			Theme.themeIndex = Theme.all.FindIndex(x=>x.name==EditorPref.Build("Zios.Theme"+Theme.suffix,"Default")).Max(0);
			Theme.active = new Theme().Use(Theme.all[Theme.themeIndex]);
			if(!lazy){
				Theme.fontsetIndex = ThemeFontset.all.FindIndex(x=>x.name==EditorPref.Build<string>("Zios.Theme.Fontset"+Theme.suffix,Theme.active.fontset.name)).Max(0);
				Theme.paletteIndex = ThemePalette.all.FindIndex(x=>x.name==EditorPref.Build<string>("Zios.Theme.Palette"+Theme.suffix,Theme.active.palette.name)).Max(0);
				Theme.skinsetIndex = ThemeSkinset.all.FindIndex(x=>x.name==EditorPref.Build<string>("Zios.Theme.Skinset"+Theme.suffix,Theme.active.skinset.name)).Max(0);
				Theme.iconsetIndex = ThemeIconset.all.FindIndex(x=>x.name==EditorPref.Build<string>("Zios.Theme.Iconset"+Theme.suffix,Theme.active.iconset.name)).Max(0);
			}
			EditorPref.Set<string>("Zios.Theme.Skinset"+Theme.suffix,Theme.active.skinset.name);
			Theme.lazyLoaded = lazy;
		}
		public static void Reset(){
			Theme.needsSetup = true;
			Theme.needsImageUpdate = true;
			Theme.needsIconUpdate = true;
		}
		public static void Refresh(){Theme.needsRefresh = true;}
		public static void Rebuild(){Theme.needsRebuild = true;}
		public static void RebuildImages(){
			Theme.needsRebuild = true;
			Theme.needsImageUpdate = true;
		}
		public static void UpdateImages(){Theme.needsImageUpdate = true;}
		public static void UpdateIcons(){Theme.needsIconUpdate = true;}
		public static void InstantReset(bool delay=false){
			Theme.needsReset = delay;
			if(delay){return;}
			Theme.Reset();
			Theme.Update();
		}
		public static void ClearStyles(){
			Time.Start();
			var types = typeof(Editor).Assembly.GetTypes();
			for(int index=0;index<types.Length;++index){
				var type = types[index];
				if(type.Name.Contains("LookDev")){continue;}
				Theme.buildTerms.ForEach(x=>type.ClearVariable(x,Reflection.staticFlags));
			}
			if(Theme.debug){Log.Group("Rebuild","  [-] Clear Styles : " + Time.Passed());}
		}
		//=================================
		// Updating
		//=================================
		public static void CheckUpdate(){
			if(Theme.separatePlaymode && Application.isPlaying){
				Theme.InstantReset(true);
			}
			else{
				Theme.ApplyIconset();
			}
		}
		public static void UpdateColors(){
			if(Theme.active.IsNull()){return;}
			RelativeColor.UpdateSystem();
			foreach(var color in Theme.active.palette.colors["*"]){
				color.Value.ApplyOffset();
				EditorPref.Set<bool>("Zios.Theme.Palette.Dark."+color.Key,color.Value.value.GetIntensity() < 0.4f);
			}
			EditorPref.Set<bool>("Zios.Theme.Palette.IsDark",Theme.active.palette.Get("Window").GetIntensity() < 0.4f);
			EditorPref.Set<bool>("EditorTheme-Dark",Theme.active.palette.Get("Window").GetIntensity() < 0.4f);
		}
		public static void ApplySettings(){
			if(Theme.all.Count < 1){return;}
			var theme = Theme.active;
			if(theme.customizablePalette && ThemePalette.all.Count > 0){
				var basePalette = ThemePalette.all[Theme.paletteIndex];
				theme.palette = new ThemePalette().Use(basePalette);
				Theme.LoadColors();
				Theme.UpdateColors();
			}
			if(theme.customizableFontset && ThemeFontset.all.Count > 0){
				var baseFontset = ThemeFontset.all[Theme.fontsetIndex];
				theme.fontset = new ThemeFontset(baseFontset).UseBuffer(theme.fontset);
				Theme.LoadFontset();
			}
			if(Theme.needsVariantReset){
				foreach(var variant in Theme.active.skinset.variants){Undo.RecordPref<bool>("Zios.Theme.Skinset.Variant"+Theme.suffix+"."+variant.name,false);}
				foreach(var variant in Theme.active.defaultVariants){Undo.RecordPref<bool>("Zios.Theme.Skinset.Variant"+Theme.suffix+"."+variant,true);}
				Theme.needsVariantReset = false;
			}
			foreach(var variant in theme.skinset.variants){
				variant.active = EditorPref.Get<bool>("Zios.Theme.Skinset.Variant"+Theme.suffix+"."+variant.name,false);
			}
			Theme.Apply();
		}
		public static void Apply(){
			if(Theme.active.IsNull()){return;}
			Time.Start();
			Theme.ApplySkinset();
			if(Theme.needsImageUpdate){
				var colors = Theme.active.palette.colors["*"].Where(x=>!x.Value.skipTexture).Select(x=>x.Value).ToList();
				Worker.Create(colors).OnStep(color=>{
					color.UpdateTexture();
					return true;
				}).Build();
				if(Theme.active.palette.swap.Count >= 1){
					var variants = Theme.active.skinset.variants.Where(x=>x.active).Select(x=>x.name).ToArray();
					var files = File.FindAll("#*.png").Where(x=>!(x.path.Contains("+")&&!variants.Contains(x.path.Parse("+","/")))).ToList();
					Worker.Create(files).OnStep(file=>{
						var texture = default(Texture2D);
						Worker.MainThread(()=>texture=file.GetAsset<Texture2D>());
						Theme.active.palette.ApplyTexture(file.path,texture);
						return true;
					}).Build();
				}
				ProxyEditor.SaveAssets();
				ProxyEditor.RefreshAssets();
			}
			Theme.needsImageUpdate = false;
			if(Theme.debug){Log.Show("[Themes-Debug] Apply -- " + Time.Passed());}
		}
		public static void Cleanup(){
			foreach(var guiSkin in Resources.FindObjectsOfTypeAll<GUISkin>()){
				if(!guiSkin.IsAsset()){
					UnityObject.DestroyImmediate(guiSkin);
				}
			}
			//Resources.UnloadUnusedAssets();
			//GC.Collect();
		}
		//=================================
		// Preferences
		//=================================
		[PreferenceItem("Themes")]
		public static void DrawPreferences(){
			EditorUI.Reset();
			if(!Theme.disabled){Theme.LoadCheck();}
			if(!Theme.separatePlaymode && ProxyEditor.IsChanging()){
				"Theme Settings are not available while in play mode unless \"Separate play mode\" active.".DrawHelp();
				return;
			}
			if(Theme.disabled){
				Theme.disabled = Theme.disabled.Draw("Disable System");
				Undo.RecordPref<bool>("Zios.Theme.Options.Disabled",Theme.disabled);
				"Disabling existing themes requires Unity to be restarted.".DrawHelp("Info");
				return;
			}
			if(Theme.active.IsNull()){
				ThemeWindow.ShowWindow();
				return;
			}
			var current = Theme.themeIndex;
			var window = EditorWindow.focusedWindow;
			if(!Theme.setupPreferences){
				Theme.PrepareFonts();
				Theme.setupPreferences = true;
			}
			if(Theme.active.name != "Default" && !window.IsNull() && window.GetType().Name.Contains("Preferences")){
				window.maxSize = new Vector2(9999999,9999999);
			}
			Undo.RecordStart<Theme>();
			Theme.undoCallback = Theme.Refresh;
			Theme.preferenceScroll = EditorGUILayout.BeginScrollView(Theme.preferenceScroll,false,false,GUI.skin.horizontalScrollbar,GUI.skin.verticalScrollbar,new GUIStyle().Padding(0,5,0,0));
			GUILayout.BeginVertical(EditorStyles.inspectorDefaultMargins.Margin(5,5,0,0));
			EditorUI.SetFieldSize(-1,200,false);
			Theme.UpdateColors();
			Theme.DrawThemes();
			Theme.DrawIconsets();
			Theme.DrawVariants();
			Theme.DrawPalettes();
			Theme.DrawFontsets();
			Theme.DrawOptions();
			GUILayout.EndVertical();
			if(current != Theme.themeIndex){
				Theme.needsVariantReset = false;
				var suffix = Theme.suffix.Remove("."+Theme.active.name);
				Undo.RecordPref<string>("Zios.Theme"+suffix,Theme.all[Theme.themeIndex].name);
				Theme.InstantReset();
				Call.Delay(Theme.Rebuild,0.25f);
				Theme.undoCallback = ()=>Theme.InstantReset(true);
			}
			else if(!Theme.needsRebuild && GUI.changed){
				Theme.Rebuild();
				Theme.undoCallback += Theme.Rebuild;
			}
			EditorGUILayout.EndScrollView();
			Undo.RecordEnd<Theme>("Theme Changes",Theme.undoCallback);
		}
		public static void DrawThemes(){
			var themeNames = Theme.all.Select(x=>x.name).ToList();
			var themeIndex = Theme.themeIndex + 1 < 2 ? 0 : Theme.themeIndex + 1;
			themeNames.Insert(1,"/");
			Theme.themeIndex = (themeNames.Draw(themeIndex,"Theme")-1).Max(0);
			GUILayout.Space(2);
		}
		public static void DrawVariants(){
			if(Theme.active.name == "Default" || Theme.active.skinset.variants.Count < 1){return;}
			var theme = Theme.active;
			var variants = theme.skinset.variants.Select(x=>x.name.ToTitleCase()).ToArray();
			var variantsActiveNames = theme.skinset.variants.Where(x=>x.active).Select(x=>x.name.ToTitleCase()).ToArray();
			//var variantsActive = theme.skinset.variants.Select(x=>x.active);
			var selected = variantsActiveNames.Join(" | ").TrimRight(" | ");
			if(selected.IsEmpty()){selected = "None";}
			GUI.SetNextControlName("Variants");
			selected.ToLabel().DrawLabel("Variants",EditorStyles.popup);
			var area = GUILayoutUtility.GetLastRect().AddX(EditorGUIUtility.labelWidth).AddWidth(-EditorGUIUtility.labelWidth);
			if(area.Clicked()){
				GUI.FocusControl("Variants");
				EditorUI.DrawMenu(variants,area,Theme.ToggleVariant,variantsActiveNames);
			}
			GUILayout.Space(2);
		}
		public static void ToggleVariant(object index){
			var variant = Theme.active.skinset.variants[index.ToInt()];
			Undo.RecordStart(variant);
			variant.active = !variant.active;
			Theme.undoCallback = ()=>{
				Theme.Refresh();
				Theme.UpdateImages();
			};
			Theme.undoCallback();
			Undo.RecordPref<bool>("Zios.Theme.Skinset.Variant"+Theme.suffix+"."+variant.name,variant.active);
			Undo.RecordEnd("Theme Changes",variant,Theme.undoCallback);
		}
		public static void DrawIconsets(){
			var theme = Theme.active;
			if(theme.customizableIconset){
				Theme.iconsetIndex = ThemeIconset.all.Select(x=>x.name).Draw(Theme.iconsetIndex,"Iconset");
				GUILayout.Space(2);
				if(EditorUI.lastChanged){
					Theme.undoCallback = ()=>{
						Theme.ApplyIconset();
						Theme.UpdateImages();
					};
					Theme.undoCallback();
				}
			}
		}
		public static void DrawPalettes(){
			var theme = Theme.active;
			int index = Theme.paletteIndex;
			bool hasPalettes = ThemePalette.all.Count > 0;
			bool paletteAltered = !theme.palette.Matches(ThemePalette.all[index]);
			if(theme.customizablePalette && hasPalettes){
				if(Theme.paletteNames.Count < 1){
					var palettePath = Theme.storagePath+"Palettes/";
					Theme.paletteNames = ThemePalette.all.Select(x=>{
						var path = x.path.Remove(palettePath,".unitypalette");
						if(x.usesSystem && RelativeColor.system == Color.clear){
							return path.Replace(path.GetPathTerm(),"/").Trim("/");
						}
						return path;
					}).ToList();
				}
				var paletteNames = Theme.paletteNames.Copy();
				var popupStyle = EditorStyles.popup;
				if(paletteAltered){
					var name = paletteNames[index];
					popupStyle = EditorStyles.popup.FontStyle("boldanditalic");
					paletteNames[index] = name + " *";
				}
				var key = "Zios.Theme.Preferences.Palette";
				EditorUI.LabelToggle(key);
				Theme.paletteIndex = paletteNames.Draw(index,"Palette",popupStyle);
				var changed = EditorUI.lastChanged;
				Theme.DrawPaletteMenu(true);
				if(EditorUI.LabelToggleEnd(key)){
					Theme.DrawColors();
				}
				GUILayout.Space(2);
				if(changed){
					Theme.undoCallback = Theme.RebuildImages;
					Theme.AdjustPalette();
				}
			}
		}
		public static void DrawPaletteMenu(bool showAdjusters=false){
			var theme = Theme.active;
			if(GUILayoutUtility.GetLastRect().Clicked(1)){
				var menu = new EditorMenu();
				var clipboard = EditorGUIUtility.systemCopyBuffer;
				menu.Add("Copy Palette",()=>EditorGUIUtility.systemCopyBuffer=theme.palette.Serialize());
				if(clipboard.Contains("[Textured]")){
					menu.Add("Paste Palette",()=>{
						Theme.RecordAction(()=>{
							theme.palette.Deserialize(clipboard);
							Theme.SaveColors();
							Theme.UpdateColors();
							Theme.UpdateImages();
							Theme.Rebuild();
						},Theme.RebuildImages);
					});
				}
				if(showAdjusters){
					menu.AddSeparator();
					menu.Add("Previous Palette &F1",Theme.PreviousPalette);
					menu.Add("Next Palette &F2",Theme.NextPalette);
				}
				else{
					menu.Add("Randomize &F3",Theme.RandomizeColors);
				}
				menu.AddSeparator();
				menu.Add("Advanced",()=>Theme.showColorsAdvanced = !Theme.showColorsAdvanced,Theme.showColorsAdvanced);
				menu.Draw();
			}
		}
		public static void DrawFontsets(){
			var theme = Theme.active;
			bool hasFontsets = ThemeFontset.all.Count > 0;
			bool fontsetAltered = !theme.fontset.Matches(ThemeFontset.all[Theme.fontsetIndex]);
			if(theme.customizableFontset && hasFontsets){
				if(Theme.fontsetNames.Count < 1){
					var fontsetsPath = Theme.storagePath+"Fontsets/";
					Theme.fontsetNames = ThemeFontset.all.Select(x=>x.path.Remove(fontsetsPath,".unityfontset").GetAssetPath()).ToList();
				}
				var fontsetNames = Theme.fontsetNames.Copy();
				var popupStyle = EditorStyles.popup;
				if(fontsetAltered){
					var name = fontsetNames[Theme.fontsetIndex];
					popupStyle = EditorStyles.popup.FontStyle("boldanditalic");
					fontsetNames[Theme.fontsetIndex] = name + " *";
				}
				var key = "Zios.Theme.Preferences.Fontset";
				EditorUI.LabelToggle(key);
				Theme.fontsetIndex = fontsetNames.Draw(Theme.fontsetIndex,"Fontset",popupStyle);
				var changed = EditorUI.lastChanged;
				Theme.DrawFontsetMenu(true);
				if(EditorUI.LabelToggleEnd(key)){
					Theme.DrawFonts();
				}
				GUILayout.Space(2);
				if(changed){
					var selectedFontset = ThemeFontset.all[Theme.fontsetIndex];
					theme.fontset = new ThemeFontset(selectedFontset).UseBuffer(theme.fontset);
					Undo.RecordPref<string>("Zios.Theme.Fontset"+Theme.suffix,selectedFontset.name);
					Theme.SaveFontset();
					Theme.Rebuild();
				}
			}
		}
		public static void DrawFontsetMenu(bool showAdjusters=false){
			var theme = Theme.active;
			if(GUILayoutUtility.GetLastRect().Clicked(1)){
				var menu = new EditorMenu();
				var clipboard = EditorGUIUtility.systemCopyBuffer;
				menu.Add("Copy Fontset",()=>EditorGUIUtility.systemCopyBuffer=theme.fontset.Serialize());
				if(clipboard.Contains("Font = ")){
					menu.Add("Paste Fontset",()=>{
						Theme.RecordAction(()=>{
							theme.fontset.Deserialize(clipboard);
							Theme.SaveFontset();
							Theme.Rebuild();
						});
					});
				}
				if(showAdjusters){
					menu.AddSeparator();
					menu.Add("Previous Fontset %F1",Theme.PreviousFontset);
					menu.Add("Next Fontset %F2",Theme.NextFontset);
				}
				menu.AddSeparator();
				menu.Add("Advanced",()=>{
					Theme.showFontsAdvanced = !Theme.showFontsAdvanced;
					Theme.SaveFontset();
					GUI.changed = false;
				},Theme.showFontsAdvanced);
				menu.Draw();
			}
		}
		public static void DrawOptions(){
			EditorUI.PadStart(-5);
			var open = "Options".ToLabel().DrawFoldout("Zios.Theme.Preferences.Options");
			if(EditorUI.lastChanged){GUI.changed=false;}
			EditorUI.PadEnd();
			if(open){
				//Theme.verticalSpacing = Theme.verticalSpacing.Draw("Vertical Spacing");
				Theme.hoverResponse = Theme.hoverResponse.Draw("Hover Response").As<HoverResponse>();
				Theme.separatePlaymode = Theme.separatePlaymode.Draw("Separate Playmode Settings");
				if(EditorUI.lastChanged){
					Undo.RecordPref<bool>("Zios.Theme.Options.SeparatePlaymode",Theme.separatePlaymode);
					Theme.InstantReset(true);
					return;
				}
				Theme.disabled = Theme.disabled.Draw("Disable System");
				if(!Theme.window.IsNull()){
					Theme.window.wantsMouseMove = Theme.hoverResponse != HoverResponse.None;
				}
				Undo.RecordPref<int>("Zios.Theme.Options.HoverResponse",Theme.hoverResponse.ToInt());
				Undo.RecordPref<bool>("Zios.Theme.Options.Disabled",Theme.disabled);
			}
			GUILayout.Space(10);
		}
		public static void DrawColors(){
			var theme = Theme.active;
			bool hasPalettes = ThemePalette.all.Count > 0;
			bool paletteAltered = !theme.palette.Matches(ThemePalette.all[Theme.paletteIndex]);
			var existingChanges = GUI.changed;
			if(theme.customizablePalette && hasPalettes){
				EditorGUI.indentLevel += 1;
				if(Theme.showColorsAdvanced){RelativeColor.autoBalance = RelativeColor.autoBalance.Draw("Autobalance").As<AutoBalance>();}
				foreach(var group in theme.palette.colors.Where(x=>x.Key!="*")){
					var groupName = group.Key;
					var isGroup = groupName != "Default";
					var colorCount = theme.palette.colors[groupName].Count(x=>x.Value.source.IsNull());
					var canExpand = Theme.showColorsAdvanced || colorCount > 3;
					if(!Theme.showColorsAdvanced && colorCount < 1){continue;}
					if(canExpand){
						var drawFoldout = groupName.ToLabel().DrawFoldout("Zios.Theme.Preferences.Palette."+groupName);
						if(EditorUI.lastChanged){GUI.changed=false;}
						if(isGroup && !drawFoldout){continue;}
						if(isGroup){
							EditorGUI.indentLevel += 1;
						}
					}
					var names = theme.palette.colors["*"].Keys.ToList();
					if(Application.platform == RuntimePlatform.WindowsEditor){
						names = "@System".AsArray().Concat(names).ToList();
					}
					foreach(var item in theme.palette.colors[groupName]){
						var color = item.Value;
						var area = new Rect(1,1,1,1);
						if(!color.sourceName.IsEmpty()){
							if(!Theme.showColorsAdvanced){continue;}
							var index = names.IndexOf(color.sourceName);
							EditorGUILayout.BeginHorizontal();
							if(index == -1){
								var message = "[" + color.sourceName + " not found]";
								index = Zios.Extensions.IEnumerableExtensions.Unshift(names,message).Draw(0,item.Key.ToTitleCase());
								if(index != 0){color.sourceName = names[index];}
							}
							else{
								color.sourceName = names[names.Draw(index,color.name.ToTitleCase())];
								EditorUI.SetLayoutOnce(35);
								if(color.blendMode == ColorBlend.Normal){color.offset = color.offset.Draw(null,null,false);}
								color.Assign(theme.palette,color.sourceName);
								if(color.blendMode != ColorBlend.Normal){
									EditorUI.SetLayoutOnce(100);
									color.blendMode = color.blendMode.Draw(null,null,false).As<ColorBlend>();
									EditorUI.SetLayoutOnce(35);
									color.offset = color.offset.Draw("",null,false).Clamp(0,1);
									EditorUI.SetLayoutOnce(80);
									color.blend = color.blend.Draw("",false);
								}
							}
							EditorGUILayout.EndHorizontal();
							area = GUILayoutUtility.GetLastRect();
							GUILayout.Space(2);
						}
						else{
							color.value = color.value.Draw(color.name.ToTitleCase());
							area = GUILayoutUtility.GetLastRect();
						}
						if(area.Clicked(1)){
							var menu = new GenericMenu();
							menu.AddItem(new GUIContent("Normal"),color.sourceName.IsEmpty(),()=>{
								color.blendMode = ColorBlend.Normal;
								color.sourceName = "";
							});
							menu.AddItem(new GUIContent("Inherited"),!color.sourceName.IsEmpty()&&color.blendMode==ColorBlend.Normal,()=>{
								color.blendMode = ColorBlend.Normal;
								if(color.sourceName.IsEmpty()){color.sourceName = names[0];}
							});
							menu.AddItem(new GUIContent("Blended"),color.blendMode!=ColorBlend.Normal,()=>{
								color.blendMode = ColorBlend.Lighten;
								if(color.sourceName.IsEmpty()){color.sourceName = names[0];}
							});
							menu.ShowAsContext();
							Event.current.Use();
						}
					}
					if(canExpand && isGroup){
						EditorGUI.indentLevel -= 1;
					}
				}
				if(paletteAltered){
					EditorGUILayout.BeginHorizontal();
					GUILayout.Space(15);
					if(GUILayout.Button("Save As",GUILayout.Width(100))){theme.palette.Export();}
					if(GUILayout.Button("Reset",GUILayout.Width(100))){Theme.LoadColors(true);}
					if(GUILayout.Button("Apply",GUILayout.Width(100))){theme.palette.Export(theme.palette.path);}
					EditorGUILayout.EndHorizontal();
				}
				if(!existingChanges && GUI.changed){
					Theme.SaveColors();
					Undo.RecordPref<int>("Zios.Theme.Palette.Autobalance",RelativeColor.autoBalance.ToInt());
					Undo.RecordPref<bool>("Zios.Theme.Palette.Advanced",Theme.showColorsAdvanced);
					Call.Delay(()=>{
						Theme.Refresh();
						Theme.UpdateImages();
					},0.1f);
					GUI.changed = false;
				}
				EditorGUI.indentLevel -=1;
			}
		}
		public static void PrepareFonts(){
			var fontPath = Theme.storagePath+"Fonts/";
			var fontFiles = File.FindAll("*.*tf").Where(x=>!x.path.Contains("Fontsets")).ToArray();
			var fontPaths = fontFiles.Select(x=>x.path);
			var fontAssets = fontFiles.Select(x=>x.GetAsset<Font>());
			Theme.builtinFonts = Locate.GetAssets<Font>().Where(x=>File.GetAssetPath(x).Contains("Library/unity")).ToArray();
			Theme.fontNames = Theme.builtinFonts.Select(x=>"@Builtin/"+x.name).Concat(fontPaths).ToList();
			Func<string,string> FixFontNames = (data)=>{
				data = data.Remove(fontPath,".ttf",".otf");
				if(data.Contains("/")){
					var folder = data.GetDirectory();
					var folderPascal = folder.ToPascalCase();
					data = folder + "/" + data.Split("/").Last().Remove(folderPascal+"-",folderPascal);
					if(Theme.fontNames.Count(x=>x.Contains(folder+"/"))==1){
						data = folder;
					}
				}
				return data.GetAssetPath().Trim("/");
			};
			Theme.fontNames = Theme.fontNames.ThreadedSelect(x=>FixFontNames(x)).ToList();
			Theme.fonts = Theme.builtinFonts.Concat(fontAssets).ToArray();
		}
		public static void DrawFonts(){
			var theme = Theme.active;
			bool hasFontsets = ThemeFontset.all.Count > 0;
			bool fontsetAltered = !theme.fontset.Matches(ThemeFontset.all[Theme.fontsetIndex]);
			var existingChanges = GUI.changed;
			if(theme.customizableFontset && hasFontsets){
				EditorGUI.indentLevel += 1;
				var fonts = Theme.fonts;
				var fontNames = Theme.fontNames.Copy();
				if(fontNames.Count < 1){fontNames.Add("No fonts found.");}
				foreach(var item in theme.fontset.fonts){
					if(item.Value.font.IsNull()){continue;}
					var themeFont = item.Value;
					var fontName = item.Key.ToTitleCase();
					var showRenderMode = Theme.showFontsAdvanced && !Theme.builtinFonts.Contains(themeFont.font);
					EditorGUILayout.BeginHorizontal();
					var index = fonts.IndexOf(themeFont.font);
					if(index == -1){
						EditorGUILayout.EndHorizontal();
						var message = "[" + themeFont.name + " not found]";
						index = Zios.Extensions.IEnumerableExtensions.Unshift(fontNames,message).Draw(0,item.Key.ToTitleCase());
						if(index != 0){themeFont.font = fonts[index-1];}
						continue;
					}
					if(showRenderMode){
						var fontPath = File.GetAssetPath(themeFont.font);
						var importer = LocateEditor.GetImporter<TrueTypeFontImporter>(fontPath);
						EditorUI.SetLayoutOnce(310);
						var mode = importer.fontRenderingMode.Draw(fontName).As<FontRenderingMode>();
						if(EditorUI.lastChanged){
							ProxyEditor.RecordObject(importer,"Font Render Mode");
							importer.fontRenderingMode = mode;
							ProxyEditor.WriteImportSettings(fontPath);
							ProxyEditor.RefreshAssets();
						}
						fontName = null;
						EditorUI.SetFieldSize(-1,1);
					}
					themeFont.font = fonts[fontNames.Draw(index,fontName,null,!showRenderMode)];
					if(Theme.showFontsAdvanced){
						EditorUI.SetFieldSize(0,35,false);
						EditorUI.SetLayout(70);
						themeFont.sizeOffset = themeFont.sizeOffset.DrawInt("Size",null,false);
						EditorUI.SetFieldSize(0,20,false);
						EditorUI.SetLayout(55);
						themeFont.offsetX = themeFont.offsetX.Draw("X",null,false);
						themeFont.offsetY = themeFont.offsetY.Draw("Y",null,false);
						EditorUI.SetLayout(0);
						EditorUI.SetFieldSize(0,200,false);
					}
					EditorGUILayout.EndHorizontal();
				}
				if(fontsetAltered){
					EditorGUILayout.BeginHorizontal();
					GUILayout.Space(15);
					if(GUILayout.Button("Save As",GUILayout.Width(100))){theme.fontset.Export();}
					if(GUILayout.Button("Reset",GUILayout.Width(100))){Theme.LoadFontset(true);}
					if(GUILayout.Button("Apply",GUILayout.Width(100))){theme.fontset.Export(theme.fontset.path);}
					EditorGUILayout.EndHorizontal();
				}
				EditorGUI.indentLevel -=1;
				if(!existingChanges && GUI.changed){Theme.SaveFontset();}
			}
		}
		//=================================
		// Skinset
		//=================================
		public static void ApplySkinset(){
			if(Theme.active.IsNull()){return;}
			var theme = Theme.active;
			theme.skinset.Apply(theme);
		}
		//=================================
		// Iconset
		//=================================
		public static void ApplyIconset(bool includeBuiltin=true){
			if(Theme.active.IsNull()){return;}
			Theme.active.iconset = ThemeIconset.all[Theme.iconsetIndex];
			if(!Theme.lazyLoaded && Theme.active.customizableIconset){
				Undo.RecordPref<string>("Zios.Theme.Iconset"+Theme.suffix,Theme.active.iconset.name);
			}
			Theme.active.iconset.Apply(includeBuiltin);
		}
		//=================================
		// Fonts
		//=================================
		public static void SaveFontset(){
			var theme = Theme.active;
			Undo.RecordPref<string>("Zios.Theme.Fontset.Settings"+Theme.suffix,theme.fontset.Serialize());
			Undo.RecordPref<bool>("Zios.Theme.Fontset.Advanced",Theme.showFontsAdvanced);
		}
		public static void LoadFontset(bool reset=false){
			var theme = Theme.active;
			if(reset){
				var original = ThemeFontset.all[Theme.fontsetIndex];
				theme.fontset = new ThemeFontset(original).UseBuffer(theme.fontset);
				return;
			}
			var value = EditorPref.Get<string>("Zios.Theme.Fontset.Settings"+Theme.suffix,"");
			theme.fontset.Deserialize(value);
		}
		[MenuItem("Edit/Themes/Development/Export/Fontset")]
		public static void ExportFontset(){Theme.active.fontset.Export();}
		//=================================
		// Colors
		//=================================
		public static void SaveColors(){
			var theme = Theme.active;
			foreach(var group in theme.palette.colors.Where(x=>x.Key!="*")){
				foreach(var color in group.Value){
					Undo.RecordPref<string>("Zios.Theme"+Theme.suffix+".Palette."+group.Key+"."+color.Key,color.Value.Serialize());
				}
			}
		}
		public static void LoadColors(bool reset=false){
			var theme = Theme.active;
			if(reset){
				var original = ThemePalette.all[Theme.paletteIndex];
				theme.palette = new ThemePalette().Use(original);
				return;
			}
			foreach(var group in theme.palette.colors.Where(x=>x.Key!="*")){
				foreach(var color in group.Value){
					var value = EditorPref.Get<string>("Zios.Theme"+Theme.suffix+".Palette."+group.Key+"."+color.Key,color.Value.Serialize());
					theme.palette.colors["*"][color.Key] = theme.palette.colors[group.Key][color.Key].Deserialize(value);
				}
			}
			foreach(var color in theme.palette.colors["*"].Copy()){
				var name = color.Value.sourceName;
				if(name.IsEmpty()){continue;}
				var source = name == "@System" ? RelativeColor.system : theme.palette.colors["*"][name];
				theme.palette.colors["*"][color.Key].Assign(source);
			}
		}
		[MenuItem("Edit/Themes/Development/Export/Palette")]
		public static void ExportPalette(){Theme.active.palette.Export();}
		//=================================
		// Shortcuts
		//=================================
		[MenuItem("Edit/Themes/Development/Reset #F1")]
		public static void DebugReset(){
			Theme.LoadCheck();
			if(Theme.debug){
				Log.Show("[Themes] Example Info message.");
				Log.Error("[Themes] Example Error message.");
				Log.Warning("[Themes] Example Warning message.");
			}
			else{
				Log.Show("[Themes] System Reset.");
			}
			Theme.InstantReset(true);
			Theme.disabled = EditorPref.Set<bool>("Zios.Theme.Options.Disabled",false);
		}
		[MenuItem("Edit/Themes/Development/Toggle Debug #F2")]
		public static void ToggleDebug(){
			Theme.debug = !Theme.debug;
			Log.Show("[Themes] Debug messages : " + Theme.debug);
		}
		[MenuItem("Edit/Themes/Development/Toggle Live Edit #F3")]
		public static void ToggleLiveEdit(){
			Theme.liveEdit = !Theme.liveEdit;
			Log.Show("[Themes] Live edit : " + Theme.liveEdit);
		}
		[MenuItem("Edit/Themes/Previous Palette &F1")]
		public static void PreviousPalette(){Theme.RecordAction(()=>Theme.AdjustPalette(-1),Theme.RebuildImages);}
		[MenuItem("Edit/Themes/Next Palette &F2")]
		public static void NextPalette(){Theme.RecordAction(()=>Theme.AdjustPalette(1),Theme.RebuildImages);}
		public static void AdjustPalette(){Theme.AdjustPalette(0);}
		public static void AdjustPalette(int adjust){
			Theme.LoadCheck();
			var theme = Theme.active;
			if(!theme.IsNull() && theme.customizablePalette){
				var usable = false;
				ThemePalette palette = null;
				while(!usable){
					Theme.paletteIndex = (Theme.paletteIndex + adjust) % ThemePalette.all.Count;
					if(Theme.paletteIndex < 0){Theme.paletteIndex = ThemePalette.all.Count-1;}
					palette = ThemePalette.all[Theme.paletteIndex];
					usable = !palette.usesSystem || (RelativeColor.system != Color.clear);
				}
				theme.palette = new ThemePalette().Use(palette);
				Undo.RecordPref<string>("Zios.Theme.Palette"+Theme.suffix,palette.name);
				Theme.SaveColors();
				Theme.UpdateImages();
				Theme.UpdateColors();
				Theme.Rebuild();
			}
		}
		[MenuItem("Edit/Themes/Development/Randomize Colors &F3")]
		public static void RandomizeColors(){
			foreach(var color in Theme.active.palette.colors["*"]){
				if(color.Value.skipTexture || !color.Value.sourceName.IsEmpty()){continue;}
				color.Value.value = Color.white.Random(0);
			}
			Theme.SaveColors();
			Theme.Refresh();
			Theme.UpdateImages();
			var time = Time.Get();
			if(Theme.colorChangeCount > 35){
				UnityEngine.Application.OpenURL("https://goo.gl/gg9609");
				Theme.colorChangeCount = -9609;
			}
			if(time < Theme.colorChangeTime){Theme.colorChangeCount += 1;}
			else if(Theme.colorChangeCount > 0){Theme.colorChangeCount = 0;}
			Theme.colorChangeTime = time + 1;
		}
		[MenuItem("Edit/Themes/Previous Fontset %F1")]
		public static void PreviousFontset(){Theme.RecordAction(()=>Theme.AdjustFontset(-1));}
		[MenuItem("Edit/Themes/Next Fontset %F2")]
		public static void NextFontset(){Theme.RecordAction(()=>Theme.AdjustFontset(1));}
		public static void AdjustFontset(int adjust){
			Theme.LoadCheck();
			var theme = Theme.active;
			if(!theme.IsNull() && theme.customizableFontset){
				Theme.fontsetIndex = (Theme.fontsetIndex + adjust) % ThemeFontset.all.Count;
				if(Theme.fontsetIndex < 0){Theme.fontsetIndex = ThemeFontset.all.Count-1;}
				var defaultFontset = ThemeFontset.all[Theme.fontsetIndex];
				theme.fontset = new ThemeFontset(defaultFontset).UseBuffer(theme.fontset);
				Undo.RecordPref("Zios.Theme.Fontset"+Theme.suffix,defaultFontset.name);
				Theme.SaveFontset();
				Theme.Rebuild();
			}
		}
		public static void RecordAction(Action method,Action callback=null){
			Undo.RecordStart(typeof(Theme));
			Theme.undoCallback = callback ?? Theme.Rebuild;
			method();
			Undo.RecordEnd("Theme Changes",typeof(Theme),Theme.undoCallback);
		}
	}
	public class ThemesAbout : EditorWindow{
		[MenuItem("Edit/Themes/About",false,1)]
		public static void Init(){
			var window = ScriptableObject.CreateInstance<ThemesAbout>();
			window.position = new Rect(100,100,1,1);
			window.minSize = window.maxSize = new Vector2(190,120);
			window.ShowAuxWindow();
		}
		public void OnGUI(){
			this.SetTitle("About Zios Themes");
			string buildText = "Build <b>"+ Theme.revision+"</b>";
			EditorGUILayout.BeginVertical(new GUIStyle().Padding(15,15,15,0));
			buildText.ToLabel().DrawLabel(EditorStyles.label.RichText(true).Clipping("Overflow").FontSize(15).Alignment("UpperCenter"));
			"Part of the <i>Zios</i> framework. Developed by Brad Smithee.".ToLabel().DrawLabel(EditorStyles.wordWrappedLabel.FontSize(12).RichText(true));
			if("Source Repository".ToLabel().DrawButton(GUI.skin.button.FixedWidth(150).Margin(12,0,5,0))){
				UnityEngine.Application.OpenURL("https://github.com/zios/unity-themes");
			}
			EditorGUILayout.EndVertical();
		}
	}
}
