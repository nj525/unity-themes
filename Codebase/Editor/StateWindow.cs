using UnityEngine;
using UnityEditor;
using Zios;
using Zios.UI;
using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using MenuFunction = UnityEditor.GenericMenu.MenuFunction;
using MenuFunction2 = UnityEditor.GenericMenu.MenuFunction2;
namespace Zios.Controller{
	public enum HeaderMode{Vertical,Horizontal,HorizontalFit}
    public class StateWindow : EditorWindow{
		//===================================
		// Data
		//===================================
		public static StateWindow Get(){return StateWindow.instance;}
		public static StateWindow instance;
	    public Table tableGUI = new Table();
		public Dictionary<TableRow,TableField[]> visibleColumns = new Dictionary<TableRow,TableField[]>();
	    public Dictionary<StateRow,int> rowIndex = new Dictionary<StateRow,int>();
		public List<string> setupSections = new List<string>();
		public Action repaintHooks = ()=>{};
	    public float nextStep;
		//===================================
		// Selection
		//===================================
		public StateTable target;
		public GameObject lastTarget;
		public StateRow[] data;
		//===================================
		// State
		//===================================
	    public int tableIndex = 0;
		public int row = -1;
		public int column = -1;
		public bool hovered;
		public bool prompted;
		//===================================
		// Visual
		//===================================
		public Vector2 scroll = Vector2.zero;
		public float cellSize;
		public float headerSize;
		public string newSection;
		//===================================
		// Unity-Specific
		//===================================
		public void OnHierarchyChange(){
			if(this.target != null){
				this.target.Refresh();
				this.BuildTable();
				this.nextStep = 0;
			}
		}
		public void Update(){
			StateWindow.instance = this;
			this.wantsMouseMove = !Application.isPlaying;
			this.CheckTarget();
			if(this.target.IsNull()){return;}
			this.CheckRebuild();
			if(Application.isPlaying){
				Events.Add("On State Update",this.Repaint,this.target.gameObject);
				Events.Add("On State Refresh",this.Repaint,this.target.gameObject);
				this.row = -1;
				this.column = -1;
			}
		}
	    public void OnGUI(){
			if(this.target.IsNull()){return;}
			//if(!Event.current.IsUseful()){return;}
			if(Event.current.type == EventType.ScrollWheel){
				this.scroll += Event.current.delta*5;
				this.Repaint();
				return;
			}
			if(!this.prompted){
				this.hovered = false;
				this.scroll = GUILayout.BeginScrollView(this.scroll);
				this.FixLabels();
				this.tableGUI.Draw();
				GUILayout.Space(10);
				GUILayout.EndScrollView();
			}
			this.CheckHotkeys();
			if(Event.current.type == EventType.MouseMove){
				this.Repaint();
			}
			if(Event.current.type == EventType.Repaint){
				this.repaintHooks();
				this.repaintHooks = ()=>{};
				if(!this.hovered){
					this.row = -1;
					this.column = -1;
				}
			}
	    }
		//===================================
		// Checks
		//===================================
		public void CheckTarget(){
			var target = Selection.activeGameObject;
			if(!target.IsNull() && (this.lastTarget != target || this.target.IsNull())){
				var table = target.GetComponent<StateTable>();
				if(!table.IsNull()){
					this.lastTarget = target;
					this.target = table;
					this.nextStep = 0;
				}
			}
		}
		public void CheckHotkeys(){
			if(Button.KeyUp("G")){this.PromptGroup();}
			if(prompted){
				int state = "Group Name?".DrawPrompt(ref this.newSection);
				if(state > 0){
					TableRow[] selected = this.tableGUI.rows.Where(x=>x.selected).ToArray();
					foreach(var row in selected){
						row.target.As<StateRow>().section = this.newSection;
					}
				}
				if(state != 0){
					this.prompted = false;
					this.newSection = "";
					this.BuildTable();
				}
			}
		}
	    public void CheckRebuild(){
			bool timeGlitch = this.nextStep-Time.realtimeSinceStartup > 2;
		    if(!Application.isPlaying && timeGlitch || Time.realtimeSinceStartup > this.nextStep){
				StateTable stateTable = this.target;
			    //stateTable.Refresh();
			    stateTable.UpdateTableList();
			    StateRow[] activeTable = stateTable.tables[this.tableIndex];
			    if(this.data != activeTable){
				    this.data = activeTable;
				    this.BuildTable();
			    }
			    if(!stateTable.advanced){
				    this.tableIndex = 0;
			    }
			    this.nextStep = Time.realtimeSinceStartup + 1f;
		   }
	    }
		//===================================
		// Operations
		//===================================
        [MenuItem ("Zios/Window/State")]
	    public static void Begin(){
		    var window = EditorWindow.GetWindow<StateWindow>();
			if(StateWindow.instance == null){
				window.position = new Rect(100,150,600,500);
			}
			window.titleContent = new GUIContent("State");
        }
		public void PromptGroup(){
			TableRow[] selected = this.tableGUI.rows.Where(x=>x.selected).ToArray();
			if(selected.Length > 0){
				string section = selected[0].target.As<StateRow>().section;
				bool sameSection = selected.Count(x=>section==x.target.As<StateRow>().section) == selected.Length;
				if(sameSection){this.newSection = section;}
				this.prompted = true;
				this.Repaint();
			}
		}
	    public void FixLabels(){
			if(this.target.tables.Count-1 < this.tableIndex){return;}
		    StateRow[] activeTable = this.target.tables[this.tableIndex];
		    if(activeTable.Length > 0){
			    this.tableGUI.GetSkin().label.fixedWidth = 0;
			    foreach(StateRow stateRow in activeTable){
				    int size = (int)(GUI.skin.label.CalcSize(new GUIContent(stateRow.name)).x) + 24;
				    size = (size / 8) * 8 + 1;
				    if(size > this.tableGUI.GetSkin().label.fixedWidth){
					    this.tableGUI.GetSkin().label.fixedWidth = size+28;
				    }
			    }
		    }
	    }
	    public virtual void BuildTable(){
		    StateTable stateTable = this.target;
		    StateRow[] activeTable = stateTable.tables[this.tableIndex];
			activeTable.OrderBy(x=>{
				if(!x.section.IsEmpty()){return x.section;}
				return x.name;
			});
			this.tableGUI = new Table();
			TableRow tableRow = this.tableGUI.AddRow();
			tableRow.AppendField(new TitleField(stateTable.gameObject.name));
			if(activeTable.Length > 0){
				this.setupSections.Clear();
				tableRow = this.tableGUI.AddRow();
				tableRow.AppendField(new HeaderField(""));
				foreach(StateRow stateRow in activeTable){
					var field = new HeaderField(stateRow);
					tableRow.AppendField(field);
				}
				foreach(StateRow stateRow in activeTable){
					if(!this.rowIndex.ContainsKey(stateRow)){
						this.rowIndex[stateRow] = 0;
					}
					int rowIndex = this.rowIndex[stateRow];
					tableRow = this.tableGUI.AddRow(stateRow);
					tableRow.AppendField(new LabelField(stateRow));
					foreach(StateRequirement requirement in stateRow.requirements[rowIndex].data){
						tableRow.AppendField(new StateField(requirement));
					}
				}
				foreach(StateRow stateRow in activeTable){
					int rowIndex = this.rowIndex[stateRow];
					string section = stateRow.section;
					if(!section.IsEmpty() && !this.setupSections.Contains(section)){
						var groupRow = new TableRow(stateRow);
						var groupLabel = new GroupLabel(section);
						groupLabel.groupRows = this.tableGUI.rows.Skip(2).Where(x=>x.target.As<StateRow>().section==section).ToArray();
						groupRow.AppendField(groupLabel);
						foreach(StateRequirement requirement in stateRow.requirements[rowIndex].data){
							var groupField = new GroupField(requirement);
							groupRow.AppendField(groupField);
							var columnFields = groupLabel.groupRows.SelectMany(x=>x.fields).Where(x=>x.order==groupField.order).Cast<StateField>().ToArray();
							groupField.columnFields = columnFields;
						}
						int insertIndex = this.tableGUI.rows.Skip(2).ToList().FindIndex(x=>x.target==stateRow);
						this.tableGUI.rows.Insert(insertIndex+2,groupRow);
						this.setupSections.Add(section);
					}
				}
				var ordered = this.tableGUI.rows.Skip(2).Where(x=>x.target is StateRow).OrderBy(x=>{
					var row = x.target.As<StateRow>();
					if(!row.section.IsEmpty()){return row.section;}
					return row.name;
				});
				this.tableGUI.rows = this.tableGUI.rows.Take(2).Concat(ordered).ToList();
				this.tableGUI.Reorder();
				this.Repaint();
		    }
	    }
		//===================================
		// Utility
		//===================================
		public static void Clip(UnityLabel label,GUIStyle style,float xClip=0,float yClip=0){
			Rect next = GUILayoutUtility.GetRect(label,style);
			StateWindow.Clip(next,label,style,xClip,yClip);
		}
		public static void Clip(Rect next,UnityLabel label,GUIStyle style,float xClip=0,float yClip=0){
			Vector2 scroll = StateWindow.Get().scroll;
			float x = next.x - scroll.x;
			float y = next.y - scroll.y;
			if(xClip == -1){next.x += scroll.x;}
			if(yClip == -1){next.y += scroll.y;}
			if(xClip > 0){style.overflow.left = (int)Mathf.Min(x-xClip,0);}
			if(yClip > 0){style.overflow.top  = (int)Mathf.Min(y-yClip,0);}
			bool xPass = xClip == -1 || (x + next.width  > xClip);
			bool yPass = yClip == -1 || (y + next.height > yClip);
			label.value.text = style.overflow.left >= -(next.width/4) ? label.value.text : "";
			label.value.text = style.overflow.top >= -(next.height/4) ? label.value.text : "";
			if(xPass && yPass){label.DrawLabel(next,style);}
		}
    }
	//===================================
	// Title Field
	//===================================
	public class TitleField : TableField{
		public TitleField(object target=null,TableRow row=null) : base(target,row){}
		public override void Draw(){
			var title = new GUIContent((string)this.target);
			var style = Style.Get("title");
			style.fixedWidth = Screen.width-24;
			Rect next = GUILayoutUtility.GetRect(title,style);
			title.DrawLabel(next.AddXY(StateWindow.Get().scroll),style);
		}
	}
	//===================================
	// Group
	//===================================
	public class GroupLabel : LabelField{
		public TableRow[] groupRows = new TableRow[0];
		public GroupLabel(object target=null,TableRow row=null) : base(target,row){}
		public override void Draw(){
			string section = this.row.target.As<StateRow>().section;
			var open = EditorPrefs.GetBool("StateWindow-GroupRow-"+section,false);
			bool allDisabled = this.groupRows.Count(x=>x.target.As<StateRow>().target.IsEnabled()) == 0;
			if(open || allDisabled){return;}
			this.DrawStyle();
			this.CheckClicked();
		}
		public override void Clicked(int button){
			if(button == 0){
				if(Event.current.control){
					this.row.selected = !this.row.selected;
				}
				else{
					string section = this.row.target.As<StateRow>().section;
					Utility.ToggleEditorPref("StateWindow-GroupRow-"+section);
					StateWindow.Get().BuildTable();
				}
			}
			if(button == 1){
				GenericMenu menu = new GenericMenu();
				GUIContent ungroup = new GUIContent("Ungroup");
				menu.AddItem(ungroup,false,new MenuFunction(this.Ungroup));
				menu.ShowAsContext();
			}
			Event.current.Use();
			StateWindow.Get().Repaint();
		}
		public void Ungroup(){
			foreach(var row in this.groupRows){
				var stateRow = (StateRow)row.target;
				stateRow.section = "";
			}
			StateWindow.Get().BuildTable();
		}
	}
	public class GroupField : StateField{
		public StateField[] columnFields = new StateField[0];
		public GroupField(object target=null,TableRow row=null) : base(target,row){}
		public int GetState(StateRequirement requirement){
			if(requirement.requireOn){return 1;}
			if(requirement.requireOff){return 2;}
			return 0;
		}
		public override void Draw(){
			var row = this.row.target.As<StateRow>();
			bool rowCollapsed = !EditorPrefs.GetBool("StateWindow-GroupRow-"+row.section,false);
			var column = StateWindow.Get().tableGUI.rows[1].fields[this.order].target.As<StateRow>();
			var columnScript = column.target;
			bool columnCollapsed = !column.section.IsEmpty() && !EditorPrefs.GetBool("StateWindow-GroupColumn-"+column.section,true);
			bool allDisabled = this.columnFields.Count(x=>x.target.As<StateRequirement>().target.IsEnabled()) == 0;
			if(!columnScript.IsEnabled() || !rowCollapsed || columnCollapsed || allDisabled){return;}
			int baseState = this.GetState(this.columnFields[0].target.As<StateRequirement>());
			bool mismatched = this.columnFields.Count(x=>this.GetState(x.target.As<StateRequirement>())!=baseState) > 0;
			this.DrawStyle(mismatched ? -1 : baseState);
			this.CheckClicked();
		}
		public override void Clicked(int button){
			int baseState = this.GetState(this.columnFields[0].target.As<StateRequirement>());
			int mismatched = this.columnFields.Count(x=>this.GetState(x.target.As<StateRequirement>())!=baseState);
			if(mismatched == 0){
				foreach(var field in this.columnFields){
					field.Clicked(button);
				}
			}
		}
	}
	//===================================
	// Header Field
	//===================================
	public class HeaderField : TableField{
		public HeaderField(object target=null,TableRow row=null) : base(target,row){}
		public override void Draw(){
			var window = StateWindow.Get();
			var label = this.target is string ? new GUIContent("") : new GUIContent(this.target.GetVariable<string>("name"));
			GUIStyle style = new GUIStyle(GUI.skin.label);
			var mode = (HeaderMode)EditorPrefs.GetInt("StateWindow-Mode",2);
			bool darkSkin = EditorGUIUtility.isProSkin;
			if(label.text == ""){
				window.visibleColumns.Clear();
				window.headerSize = 64;
				style.margin.left = 5;
				string background = darkSkin ? "BoxBlackA30" : "BoxWhiteBWarm";
				style.normal.background = FileManager.GetAsset<Texture2D>(background);
				if(mode == HeaderMode.Vertical){
					window.headerSize = 35;
					style.fixedHeight = style.fixedWidth;
					StateWindow.Clip(label,style,0,window.headerSize);
				}
				if(mode != HeaderMode.Vertical){StateWindow.Clip(label,style,-1,-1);}
				return;
			}
			var row = this.target.As<StateRow>();
			var columnCollapsed = !row.section.IsEmpty() &&!EditorPrefs.GetBool("StateWindow-GroupColumn-"+row.section,true);
			var script = row.target;
			if(!script.IsEnabled() || columnCollapsed){return;}
			bool hovered = window.column == this.order;
			if(hovered){
				string background = darkSkin ? "BoxBlackHighlightBlueAWarm" : "BoxBlackHighlightBlueDWarm";
				style.normal.textColor = darkSkin ? Colors.Get("ZestyBlue") : Colors.Get("White");
				style.normal.background = FileManager.GetAsset<Texture2D>(background);
			}
			if(mode == HeaderMode.Vertical){
				float halfWidth = style.fixedWidth / 2;
				float halfHeight = style.fixedHeight / 2;
				GUIStyle rotated = new GUIStyle(style).Rotate90();
				Rect last = GUILayoutUtility.GetRect(new GUIContent(""),rotated);
				GUIUtility.RotateAroundPivot(90,last.center);
				Rect position = new Rect(last.x,last.y,0,0);
				position.x +=  halfHeight-halfWidth;
				position.y += -halfHeight+halfWidth;
				style.overflow.left = (int)-window.scroll.y;
				label.text = style.overflow.left >= -(position.width/4)-9 ? label.text : "";
				label.DrawLabel(position,style);
				GUI.matrix = Matrix4x4.identity;
			}
			else{
				var visible = window.visibleColumns;
				if(!visible.ContainsKey(this.row)){
					Func<TableField,bool> isEnabled = x=>x.target.As<StateRow>().target.IsEnabled();
					visible[this.row] = this.row.fields.Skip(1).Where(isEnabled).Cast<TableField>().ToArray();
				}
				style.margin.right = visible[this.row].Last() == this ? 13 : style.margin.right;
				float area = window.cellSize = ((Screen.width-style.fixedWidth-17)/visible[this.row].Length);
				area = window.cellSize = Mathf.Floor(area-2);
				style.fixedWidth = mode == HeaderMode.HorizontalFit ? area : style.fixedWidth;
				style.alignment = TextAnchor.MiddleCenter;
				StateWindow.Clip(label,style,GUI.skin.label.fixedWidth+7,-1);
			}
			this.CheckClicked();
		}
		public override void Clicked(int button){
			if(button == 0){
				int mode = (EditorPrefs.GetInt("StateWindow-Mode",2)+1)%3;
				EditorPrefs.SetInt("StateWindow-Mode",mode);
				StateWindow.Get().Repaint();
				return;
			}
			/*GenericMenu menu = new GenericMenu();
			GUIContent toggleUpdateText = new GUIContent(" Always Update");
			MenuFunction toggleUpdate = ()=>Utility.ToggleEditorPref("StateWindow-AlwaysUpdate");
			menu.AddItem(toggleUpdateText,false,toggleUpdate);
			menu.ShowAsContext();*/
		}
	}
	//===================================
	// Label Field
	//===================================
	public class LabelField : TableField{
		public LabelField(object target=null,TableRow row=null) : base(target,row){}
		public override void Draw(){
			var row = this.row.target.As<StateRow>();
			var script = row.target;
			if(!script.IsEnabled()){return;}
			if(!row.section.IsEmpty()){
				var open = EditorPrefs.GetBool("StateWindow-GroupRow-"+row.section,false);
				if(!open){return;}
			}
			this.DrawStyle();
			this.CheckClicked();
		}
		public virtual void DrawStyle(){
			var window = StateWindow.Get();
			var row = this.row.target.As<StateRow>();
			var script = row.target;
			bool darkSkin = EditorGUIUtility.isProSkin;
			string name = this.target is string ? (string)this.target : script.alias;
			string background = darkSkin ? "BoxBlackA30" : "BoxWhiteBWarm";
			Color textColor = darkSkin? Colors.Get("Silver") : Colors.Get("Black");
			GUIContent content = new GUIContent(name);
			GUIStyle style = new GUIStyle(GUI.skin.label);
			style.margin.left = 5;
			bool hovered = window.row == this.row.order;
			if(hovered){
				textColor = darkSkin ? Colors.Get("ZestyBlue") : Colors.Get("White");
				background = darkSkin ? "BoxBlackHighlightBlueAWarm" : "BoxBlackHighlightBlueDWarm";
			}
			if(this.row.selected){
				textColor = darkSkin ? Colors.Get("White") : Colors.Get("White");
				background = darkSkin ? "BoxBlackHighlightPurpleAWarm" : "BoxBlackHighlightPurpleDWarm";
			}
			if(Application.isPlaying){
				textColor = Colors.Get("Gray");
				background = darkSkin ? "BoxBlackAWarm30" : "BoxWhiteBWarm50";
				if(script.usable){
					textColor = darkSkin ? Colors.Get("Silver") : Colors.Get("Black");
					background = darkSkin ? "BoxBlackA30" : "BoxWhiteBWarm";
				}
				if(script.used){
					textColor = darkSkin ? Colors.Get("White") : Colors.Get("White");
					background = darkSkin ? "BoxBlackHighlightYellowAWarm" : "BoxBlackHighlightYellowDWarm";
				}
				if(script.inUse){
					textColor = darkSkin ? Colors.Get("White") : Colors.Get("White");
					background = darkSkin ? "BoxBlackHighlightPurpleAWarm" : "BoxBlackHighlightPurpleDWarm";
				}
			}
			if(!row.section.IsEmpty()){
				GUIStyle expand = Style.Get("buttonExpand");
				bool open = EditorPrefs.GetBool("StateWindow-GroupRow-"+row.section,false);
				string symbol = open ? "-" : "+";
				StateWindow.Clip(symbol,expand,-1,window.headerSize);
				if(GUILayoutUtility.GetLastRect().Clicked()){
					window.repaintHooks += ()=>{
						Utility.ToggleEditorPref("StateWindow-GroupRow-"+row.section);
						window.BuildTable();
					};
					window.Repaint();
				}
				style.fixedWidth -= 28;
				style.margin.left = 0;
			}
			style.normal.textColor = textColor;
			style.normal.background = FileManager.GetAsset<Texture2D>(background);
			StateWindow.Clip(content,style,-1,window.headerSize);
		}
		public override void Clicked(int button){
			var stateRow = (StateRow)this.row.target;
			int rowIndex = StateWindow.Get().rowIndex[stateRow];
			if(button == 0){
				if(Event.current.control){
					this.row.selected = !this.row.selected;
				}
				else if(stateRow.requirements.Length > 1){
					int length = stateRow.requirements.Length;
					rowIndex += Event.current.alt ? -1 : 1;
					if(rowIndex < 0){rowIndex = length-1;}
					if(rowIndex >= length){rowIndex = 0;}
					StateWindow.Get().rowIndex[stateRow] = rowIndex;
					StateWindow.Get().BuildTable();
				}
			}
			if(button == 1){
				GenericMenu menu = new GenericMenu();
				GUIContent addAlternative = new GUIContent("Add Alternate Row");
				GUIContent removeAlternative = new GUIContent("Remove Alternative Row");
				TableRow[] selected = this.row.table.rows.Where(x=>x.selected).ToArray();
				if(selected.Length > 0){
					GUIContent groupContent = new GUIContent("Group");
					menu.AddItem(groupContent,false,new MenuFunction(StateWindow.Get().PromptGroup));
				}
				menu.AddItem(addAlternative,false,new MenuFunction2(this.AddAlternativeRow),stateRow);
				if(rowIndex != 0){
					menu.AddItem(removeAlternative,false,new MenuFunction2(this.RemoveAlternativeRow),stateRow);
				}
				menu.ShowAsContext();
			}
			Event.current.Use();
			StateWindow.Get().Repaint();
		}
	    public void AddAlternativeRow(object target){
		    StateRow row = (StateRow)target;
		    List<StateRowData> data = new List<StateRowData>(row.requirements);
		    data.Add(new StateRowData());
		    row.requirements = data.ToArray();
		    StateWindow.Get().target.Refresh();
		    StateWindow.Get().rowIndex[row] = row.requirements.Length-1;
		    StateWindow.Get().BuildTable();
	    }
	    public void RemoveAlternativeRow(object target){
		    StateRow row = (StateRow)target;
			int rowIndex = StateWindow.Get().rowIndex[row];
		    List<StateRowData> data = new List<StateRowData>(row.requirements);
		    data.RemoveAt(rowIndex);
		    row.requirements = data.ToArray();
			StateWindow.Get().rowIndex[row] = rowIndex-1;
		    StateWindow.Get().BuildTable();
	    }
	}
	//===================================
	// State Field
	//===================================
	public class StateField : TableField{
		public StateField(object target=null,TableRow row=null) : base(target,row){}
		public override void Draw(){
			var row = this.row.target.As<StateRow>();
			var rowScript = row.target;
			var rowCollapsed = !row.section.IsEmpty() && !EditorPrefs.GetBool("StateWindow-GroupRow-"+row.section,false);
			/*var column = StateWindow.Get().tableGUI.rows[1].fields[this.order].target.As<StateRow>();
			var columnScript = this.target.As<StateRequirement>().target;
			var columnCollapsed = !column.section.IsEmpty() && !EditorPrefs.GetBool("StateWindow-GroupColumn-"+column.section,true);*/
			if(!rowScript.IsEnabled() /*|| !columnScript.IsEnabled()*/ || rowCollapsed /*|| columnCollapsed*/){return;}
			int state = 0;
			var requirement = (StateRequirement)this.target;
			if(requirement.requireOn){state = 1;}
			if(requirement.requireOff){state = 2;}
			this.DrawStyle(state);
			this.CheckClicked();
		}
		public virtual void DrawStyle(int state=0){
			string value = "";
			var row = this.row.target.As<StateRow>();
			var window = StateWindow.Get();
			int rowIndex = window.rowIndex[row];
			var mode = (HeaderMode)EditorPrefs.GetInt("StateWindow-Mode",2);
			GUIStyle style = new GUIStyle(GUI.skin.button);
			if(Application.isPlaying){style.hover = style.normal;}
			if(state == -1){
				value = rowIndex != 0 ? rowIndex.ToString() : "";
				style = Style.Get("buttonDisabled",true);
			}
			else if(state == 1){
				value = rowIndex != 0 ? rowIndex.ToString() : "";
				style = Style.Get("buttonOn",true);
			}
			else if(state == 2){
				value = rowIndex != 0 ? rowIndex.ToString() : "";
				style = Style.Get("buttonOff",true);
			}
			style.fixedWidth = mode == HeaderMode.Horizontal ? GUI.skin.label.fixedWidth : GUI.skin.label.fixedHeight;
			if(mode == HeaderMode.HorizontalFit){
				var visible = window.visibleColumns;
				if(!visible.ContainsKey(this.row)){
					Func<TableField,bool> isEnabled = x=>x.target.As<StateRequirement>().target.IsEnabled();
					visible[this.row] = this.row.fields.Skip(1).Where(isEnabled).Cast<TableField>().ToArray();
				}
				style.margin.right = visible[this.row].Last() == this ? 13 : style.margin.right;
				style.fixedWidth = window.cellSize;
			}
			float headerSize = window.headerSize;
			StateWindow.Clip(value,style,GUI.skin.label.fixedWidth+7,headerSize);
			if(!Application.isPlaying && GUILayoutUtility.GetLastRect().Hovered()){
				window.row = this.row.order;
				window.column = this.order;
				StateWindow.Get().hovered = true;
			}
		}
		public override void Clicked(int button){
			int state = 0;
			var requirement = (StateRequirement)this.target;
			if(requirement.requireOn){state = 1;}
			if(requirement.requireOff){state = 2;}
			int amount = button == 0 ? 1 : -1;
			state += amount;
			state = state.Modulus(3);
			requirement.requireOn = false;
			requirement.requireOff = false;
			if(state == 1){requirement.requireOn = true;}
			if(state == 2){requirement.requireOff = true;}
			Utility.SetDirty(StateWindow.Get().target,false,true);
			//StateWindow.Get().target.UpdateStates();
			StateWindow.Get().Repaint();
		}
	}
}