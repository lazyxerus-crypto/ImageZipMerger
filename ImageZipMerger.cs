using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;

class PageItem {
 public string Archive, Entry; public int Index; public bool Removed;
 public override string ToString() { return (Removed ? "[제외] " : "") + Entry; }
}
class Episode {
 public string Name; public List<PageItem> Pages = new List<PageItem>();
 public override string ToString() { return Name + "  (" + Pages.Count(p => !p.Removed) + "장)"; }
}
class Natural : IComparer<string> {
 [DllImport("shlwapi.dll", CharSet=CharSet.Unicode)] static extern int StrCmpLogicalW(string a,string b);
 public int Compare(string a,string b) { return StrCmpLogicalW(a,b); }
}
class Merger : Form {
 [DllImport("user32.dll")] static extern int GetScrollPos(IntPtr window,int bar);
 [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr window,int message,IntPtr wParam,IntPtr lParam);
 static void RestoreHorizontal(ListBox list,int position) {SendMessage(list.Handle,0x114,new IntPtr((position<<16)|4),IntPtr.Zero);}
 EpisodeListBox episodes = new EpisodeListBox(); PageListBox pages = new PageListBox();
 PictureBox preview = new PictureBox(); TextBox name = new TextBox();
 Label status = new Label(), detail = new Label(); CheckBox excluded = new CheckBox();
 List<Episode> data = new List<Episode>(); Stack<List<PageItem>> undo = new Stack<List<PageItem>>();
 bool refreshing, busy;
 readonly ToolTip fileTips=new ToolTip {InitialDelay=350,ReshowDelay=100,AutoPopDelay=20000,ShowAlways=true};
 Point dragStart; Episode dragEpisode; int insertAt=-1;
 readonly string dragFormat="ImageZipMerger.Episode."+Guid.NewGuid().ToString("N");
 Point pageDragStart; List<PageItem> draggedPages; Episode pageDragEpisode; int pageInsertAt=-1;
 readonly string pageDragFormat="ImageZipMerger.Page."+Guid.NewGuid().ToString("N");
 public Merger() {
  Text="이미지 ZIP 합치기 v1.9"; Size=new Size(1180,800); MinimumSize=new Size(950,620); Font=new Font("맑은 고딕",10); StartPosition=FormStartPosition.CenterScreen; BackColor=Color.FromArgb(245,247,250); AutoScaleMode=AutoScaleMode.Font;
  var root=new TableLayoutPanel { Dock=DockStyle.Fill, RowCount=3, ColumnCount=1, Padding=new Padding(12) };
  root.RowStyles.Add(new RowStyle(SizeType.Absolute,48)); root.RowStyles.Add(new RowStyle(SizeType.Percent,100)); root.RowStyles.Add(new RowStyle(SizeType.Absolute,42)); Controls.Add(root);
  var top=new FlowLayoutPanel {Dock=DockStyle.Fill,WrapContents=false}; root.Controls.Add(top,0,0);
  AddButton(top,"압축파일 추가",AddFiles); AddButton(top,"ZIP으로 저장",Save); AddButton(top,"삭제 취소",Undo);
  top.Controls.Add(new Label {Text="ZIP / CBZ · 파일을 창으로 끌어와도 됩니다",AutoSize=true,Padding=new Padding(12,9,0,0)});
  var body=new TableLayoutPanel {Dock=DockStyle.Fill, ColumnCount=3,RowCount=1}; body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,38)); body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,20)); body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,42)); root.Controls.Add(body,0,1);
  var left=Column(body,0,"1. 회차 선택","빈 공간 드래그로 여러 회차 선택 · Delete로 빼기");
  episodes.Dock=DockStyle.Fill; episodes.IntegralHeight=false; episodes.HorizontalScrollbar=true; episodes.SelectionMode=SelectionMode.MultiExtended; left.Controls.Add(episodes,0,1); EnableEpisodeDrag();
  var epControls=Footer(left);
  var renameRow=Grid(2); renameRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));renameRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,90));
  name.Dock=DockStyle.Fill;name.Anchor=AnchorStyles.Left|AnchorStyles.Right;name.Margin=new Padding(0,7,6,0);renameRow.Controls.Add(name,0,0);renameRow.Controls.Add(ActionButton("이름 적용",Rename),1,0);epControls.Controls.Add(renameRow,0,0);
  var orderRow=Grid(3); for(int i=0;i<3;i++)orderRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,33.333f));
  orderRow.Controls.Add(ActionButton("↑ 위로",delegate {MoveEpisode(-1);}),0,0);orderRow.Controls.Add(ActionButton("↓ 아래로",delegate {MoveEpisode(1);}),1,0);orderRow.Controls.Add(ActionButton("회차 빼기",RemoveEpisode),2,0);epControls.Controls.Add(orderRow,0,1);
  epControls.Controls.Add(Hint("아래 가로 스크롤을 끌면 이름 끝까지 볼 수 있습니다."),0,2);
  var middle=Column(body,1,"2. 이미지 선택","Ctrl / Shift로 여러 장 선택"); pages.Dock=DockStyle.Fill; pages.IntegralHeight=false; pages.HorizontalScrollbar=true; pages.SelectionMode=SelectionMode.MultiExtended; middle.Controls.Add(pages,0,1);
  var pgControls=Footer(middle);var actions=Grid(2);actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));actions.Controls.Add(ActionButton("삭제",Delete),0,0);actions.Controls.Add(ActionButton("복원",Restore),1,0);pgControls.Controls.Add(actions,0,0);
  var pageOrder=Grid(2);pageOrder.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));pageOrder.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));pageOrder.Controls.Add(ActionButton("↑ 위로",delegate {MovePages(-1);}),0,0);pageOrder.Controls.Add(ActionButton("↓ 아래로",delegate {MovePages(1);}),1,0);pgControls.Controls.Add(pageOrder,0,1);
  excluded.Text="제외 이미지 표시";excluded.AutoSize=false;excluded.Dock=DockStyle.Fill;excluded.Margin=new Padding(0);pgControls.Controls.Add(excluded,0,2);excluded.CheckedChanged+=delegate {RefreshPages();};
  fileTips.SetToolTip(pageOrder,"선택한 이미지들을 함께 이동합니다. Alt+↑ / Alt+↓로도 이동할 수 있습니다.");
  var right=Column(body,2,"3. 이미지 미리보기","회차 선택 시 첫 페이지 자동 표시"); preview.Dock=DockStyle.Fill; preview.SizeMode=PictureBoxSizeMode.Zoom; preview.BackColor=Color.FromArgb(232,236,242); right.Controls.Add(preview,0,1);detail.Dock=DockStyle.Fill;detail.Padding=new Padding(0,10,0,0);detail.AutoEllipsis=true;right.Controls.Add(detail,0,2);
  status.Dock=DockStyle.Fill; status.TextAlign=ContentAlignment.MiddleLeft; root.Controls.Add(status,0,2);
  EnableFileTips(episodes); EnableFileTips(pages);EnablePageDrag();
  episodes.SelectedIndexChanged+=delegate {if(!refreshing && !episodes.SelectingArea) {name.Text=Current==null ? "" : Current.Name; RefreshPages();}};
  episodes.AreaSelectionFinished+=delegate {name.Text=Current==null?"":Current.Name;RefreshPages();UpdateStatus();};
  episodes.KeyDown+=delegate(object sender,KeyEventArgs e) {if(busy)return;if(e.KeyCode==Keys.Delete){RemoveEpisode();e.SuppressKeyPress=true;}if(e.Control && e.KeyCode==Keys.A){refreshing=true;for(int i=0;i<episodes.Items.Count;i++)episodes.SetSelected(i,true);refreshing=false;name.Text=Current==null?"":Current.Name;RefreshPages();e.SuppressKeyPress=true;}};
  pages.SelectedIndexChanged+=delegate {if(!refreshing)ShowPreview();}; pages.KeyDown+=delegate(object s,KeyEventArgs e) {if(busy)return;if(e.Alt && (e.KeyCode==Keys.Up || e.KeyCode==Keys.Down)){MovePages(e.KeyCode==Keys.Up?-1:1);e.SuppressKeyPress=true;return;}if(e.KeyCode==Keys.Delete) {Delete();e.SuppressKeyPress=true;} if(e.Control && e.KeyCode==Keys.A) {refreshing=true;try {for(int i=0;i<pages.Items.Count;i++) pages.SetSelected(i,true);}finally {refreshing=false;}ShowPreview();e.SuppressKeyPress=true;}};
  AllowDrop=true; DragEnter+=delegate(object s,DragEventArgs e) {if(!busy && e.Data.GetDataPresent(DataFormats.FileDrop)) e.Effect=DragDropEffects.Copy;}; DragDrop+=delegate(object s,DragEventArgs e) {if(!busy) LoadFiles((string[])e.Data.GetData(DataFormats.FileDrop));};
  FormClosing+=delegate(object s,FormClosingEventArgs e) {if(busy) {e.Cancel=true; MessageBox.Show("저장이 끝난 뒤 닫아 주세요.");}}; UpdateStatus();
 }
 void EnableFileTips(ListBox list) {
  list.MouseMove+=delegate(object sender,MouseEventArgs e) {
   string text="";int i=list.IndexFromPoint(e.Location);
   if(e.Button==MouseButtons.None && i>=0) {
    var ep=list.Items[i] as Episode;var page=list.Items[i] as PageItem;
    if(ep!=null)text="회차: "+ep.Name+(ep.Pages.Count==0?"":"\n파일: "+Path.GetFileName(ep.Pages[0].Archive));
    else if(page!=null)text=page.Entry;
   }
   if(fileTips.GetToolTip(list)!=text)fileTips.SetToolTip(list,text);
  };
  list.MouseLeave+=delegate {fileTips.SetToolTip(list,"");};
  list.MouseDown+=delegate {fileTips.SetToolTip(list,"");};
  list.MouseWheel+=delegate {fileTips.SetToolTip(list,"");};
 }
 protected override void Dispose(bool disposing) {if(disposing)fileTips.Dispose();base.Dispose(disposing);}
 TableLayoutPanel Grid(int columns) {return new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=columns,RowCount=1,Margin=new Padding(0),Padding=new Padding(0)};}
 Label Hint(string text) {return new Label {Text=text,Dock=DockStyle.Fill,ForeColor=Color.FromArgb(96,107,123),Font=new Font(Font.FontFamily,9),Margin=new Padding(0),Padding=new Padding(0,5,0,0)};}
 Button ActionButton(string text,Action action) {var b=new Button {Text=text,Dock=DockStyle.Fill,Margin=new Padding(0,2,5,2),FlatStyle=FlatStyle.Flat,BackColor=Color.White};b.FlatAppearance.BorderColor=Color.FromArgb(204,211,222);b.Click+=delegate {if(!busy)action();};return b;}
 TableLayoutPanel Footer(TableLayoutPanel parent) {var p=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=1,RowCount=3,Margin=new Padding(0),Padding=new Padding(0,8,0,0)};p.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));p.RowStyles.Add(new RowStyle(SizeType.Absolute,38));p.RowStyles.Add(new RowStyle(SizeType.Absolute,38));p.RowStyles.Add(new RowStyle(SizeType.Percent,100));parent.Controls.Add(p,0,2);return p;}
 TableLayoutPanel Column(TableLayoutPanel parent,int index,string title,string subtitle) {var p=new TableLayoutPanel {Dock=DockStyle.Fill,RowCount=3,ColumnCount=1,Padding=new Padding(4)};p.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));p.RowStyles.Add(new RowStyle(SizeType.Absolute,62));p.RowStyles.Add(new RowStyle(SizeType.Percent,100));p.RowStyles.Add(new RowStyle(SizeType.Absolute,148));var header=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=1,RowCount=2,Margin=new Padding(0)};header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));header.RowStyles.Add(new RowStyle(SizeType.Absolute,29));header.RowStyles.Add(new RowStyle(SizeType.Percent,100));header.Controls.Add(new Label {Text=title,Dock=DockStyle.Fill,Font=new Font(Font,FontStyle.Bold),Margin=new Padding(0),TextAlign=ContentAlignment.MiddleLeft},0,0);header.Controls.Add(Hint(subtitle),0,1);p.Controls.Add(header,0,0);parent.Controls.Add(p,index,0);return p;}
 void AddButton(Control p,string label,Action action) {var b=new Button {Text=label,AutoSize=true,Height=34,MinimumSize=new Size(44,34)}; b.Click+=delegate {if(!busy) action();}; p.Controls.Add(b);}
 Episode Current { get {return episodes.SelectedItem as Episode;} }
 void AddFiles() {using(var d=new OpenFileDialog {Filter="이미지 압축파일|*.zip;*.cbz",Multiselect=true}) if(d.ShowDialog()==DialogResult.OK) LoadFiles(d.FileNames);}
 public static Episode ReadArchive(string path) {
  var ep=new Episode {Name=Path.GetFileNameWithoutExtension(path)};
  using(var z=ZipFile.OpenRead(path)) for(int i=0;i<z.Entries.Count;i++) {var e=z.Entries[i]; string ext=Path.GetExtension(e.FullName).ToLowerInvariant(); if(e.Name.Length>0 && !e.FullName.StartsWith("__MACOSX/") && new[]{".jpg",".jpeg",".png",".gif",".bmp",".webp",".tif",".tiff",".avif"}.Contains(ext)) ep.Pages.Add(new PageItem {Archive=Path.GetFullPath(path),Entry=e.FullName,Index=i});}
  ep.Pages=ep.Pages.OrderBy(p=>p.Entry,new Natural()).ToList(); return ep;
 }
 void LoadFiles(string[] paths) {var errors=new List<string>(); foreach(string path in paths) try {var ep=ReadArchive(path); if(ep.Pages.Count==0) errors.Add(Path.GetFileName(path)+": 이미지 없음"); else data.Add(ep);} catch(Exception ex) {errors.Add(Path.GetFileName(path)+": "+ex.Message);} RefreshEpisodes(data.Count-1); if(errors.Count>0) MessageBox.Show(string.Join("\n",errors),"불러오기 안내");}
 void RefreshEpisodes(int selected) {RefreshEpisodesAtPosition(selected,false);}
 void RefreshEpisodesAtPosition(int selected,bool keepPosition) {
  int top=episodes.TopIndex,horizontal=GetScrollPos(episodes.Handle,0);var previous=Current;
  fileTips.SetToolTip(episodes,"");refreshing=true;episodes.BeginUpdate();
  try {
   episodes.Items.Clear();episodes.Items.AddRange(data.ToArray());
   episodes.HorizontalExtent=data.Count==0?0:data.Max(ep=>TextRenderer.MeasureText(ep.ToString(),episodes.Font,Size.Empty,TextFormatFlags.SingleLine|TextFormatFlags.NoPrefix).Width)+12;
   if(data.Count>0) {episodes.SelectedIndex=Math.Max(0,Math.Min(selected,data.Count-1));if(keepPosition)episodes.TopIndex=Math.Min(top,data.Count-1);}
  } finally {episodes.EndUpdate();RestoreHorizontal(episodes,horizontal);refreshing=false;}
  name.Text=Current==null?"":Current.Name;RefreshPagesAtPosition(keepPosition && Current==previous);
 }
 void RefreshPages() {RefreshPagesAtPosition(false);}
 void RefreshPagesAtPosition(bool keepPosition) {
  int top=pages.TopIndex,index=pages.SelectedIndex,horizontal=GetScrollPos(pages.Handle,0);var previous=pages.SelectedItem;
  fileTips.SetToolTip(pages,"");refreshing=true;pages.BeginUpdate();
  try {
   pages.Items.Clear();if(Current!=null)pages.Items.AddRange(Current.Pages.Where(p=>excluded.Checked || !p.Removed).ToArray());UpdatePageExtent();
   if(pages.Items.Count>0) {
    int target=keepPosition && previous!=null?pages.Items.IndexOf(previous):-1;
    if(target<0)target=keepPosition?Math.Max(0,Math.Min(index,pages.Items.Count-1)):0;
    pages.SelectedIndex=target;
    if(keepPosition)pages.TopIndex=Math.Min(top,pages.Items.Count-1);
   }
  } finally {pages.EndUpdate();RestoreHorizontal(pages,horizontal);refreshing=false;}
  ShowPreview();UpdateStatus();
 }
 void EnableEpisodeDrag() {
  episodes.AllowDrop=true; episodes.DrawMode=DrawMode.OwnerDrawFixed; episodes.ItemHeight=Font.Height+4;
  episodes.DrawItem+=delegate(object sender,DrawItemEventArgs e) {
   if(e.Index<0)return;e.DrawBackground();
   TextRenderer.DrawText(e.Graphics,episodes.Items[e.Index].ToString(),e.Font,e.Bounds,e.ForeColor,TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.SingleLine|TextFormatFlags.NoPrefix);
   e.DrawFocusRectangle();
   using(var pen=new Pen(Color.DodgerBlue,3)) {
    if(insertAt==e.Index)e.Graphics.DrawLine(pen,e.Bounds.Left,e.Bounds.Top+1,e.Bounds.Right,e.Bounds.Top+1);
    if(insertAt==episodes.Items.Count && e.Index==episodes.Items.Count-1)e.Graphics.DrawLine(pen,e.Bounds.Left,e.Bounds.Bottom-2,e.Bounds.Right,e.Bounds.Bottom-2);
   }
  };
  episodes.MouseDown+=delegate(object sender,MouseEventArgs e) {dragEpisode=null;if(e.Button!=MouseButtons.Left || busy)return;int i=episodes.IndexFromPoint(e.Location);if(i>=0){dragEpisode=data[i];dragStart=e.Location;}};
  episodes.MouseUp+=delegate {dragEpisode=null;};
  episodes.MouseMove+=delegate(object sender,MouseEventArgs e) {
   if(busy || episodes.SelectingArea || dragEpisode==null || e.Button!=MouseButtons.Left || ModifierKeys!=Keys.None)return;
   var box=new Rectangle(dragStart.X-SystemInformation.DragSize.Width/2,dragStart.Y-SystemInformation.DragSize.Height/2,SystemInformation.DragSize.Width,SystemInformation.DragSize.Height);
   if(box.Contains(e.Location))return;
   try {episodes.DoDragDrop(new DataObject(dragFormat,"move"),DragDropEffects.Move);}finally{dragEpisode=null;insertAt=-1;episodes.Invalidate();UpdateStatus();}
  };
  episodes.DragEnter+=EpisodeDragOver; episodes.DragOver+=EpisodeDragOver;
  episodes.DragLeave+=delegate {insertAt=-1;episodes.Invalidate();};
  episodes.DragDrop+=delegate(object sender,DragEventArgs e) {
   if(busy)return;
   if(e.Data.GetDataPresent(DataFormats.FileDrop)){LoadFiles((string[])e.Data.GetData(DataFormats.FileDrop));return;}
   if(dragEpisode!=null && e.Data.GetDataPresent(dragFormat))DropEpisode(dragEpisode,EpisodeInsertion(episodes.PointToClient(new Point(e.X,e.Y))));
   insertAt=-1;episodes.Invalidate();
  };
 }
 int EpisodeInsertion(Point p) {int i=episodes.IndexFromPoint(p);if(i<0)return p.Y<0?episodes.TopIndex:data.Count;var rect=episodes.GetItemRectangle(i);return p.Y<rect.Top+rect.Height/2?i:i+1;}
 void EpisodeDragOver(object sender,DragEventArgs e) {
  e.Effect=DragDropEffects.None;if(busy)return;
  if(e.Data.GetDataPresent(DataFormats.FileDrop)){e.Effect=DragDropEffects.Copy;return;}
  if(dragEpisode==null || !e.Data.GetDataPresent(dragFormat))return;
  e.Effect=DragDropEffects.Move;Point p=episodes.PointToClient(new Point(e.X,e.Y));
  if(p.Y<episodes.ItemHeight && episodes.TopIndex>0)episodes.TopIndex--;
  else if(p.Y>episodes.ClientSize.Height-episodes.ItemHeight && episodes.TopIndex<data.Count-1)episodes.TopIndex++;
  insertAt=EpisodeInsertion(p);episodes.Invalidate();status.Text="파란 선 위치에 놓으면 회차 순서가 바뀝니다.";
 }
 void DropEpisode(Episode item,int position) {
  int from=data.IndexOf(item);if(from<0)return;position=Math.Max(0,Math.Min(position,data.Count));if(position>from)position--;
  data.RemoveAt(from);data.Insert(position,item);RefreshEpisodes(position);
 }
 void UpdateStatus() {status.Text=string.Format("회차 {0}개 · 저장할 이미지 {1}장 · 제외 {2}장    |    원본 파일은 변경하지 않습니다.",data.Count,data.Sum(e=>e.Pages.Count(p=>!p.Removed)),data.Sum(e=>e.Pages.Count(p=>p.Removed)))+"  |  선택 회차 "+episodes.SelectedItems.Count+"개";}
 void Rename() {if(Current!=null && name.Text.Trim().Length>0) {Current.Name=name.Text.Trim(); RefreshEpisodes(episodes.SelectedIndex);}}
 void MoveEpisode(int delta) {int i=episodes.SelectedIndex,j=i+delta;if(i<0 || j<0 || j>=data.Count)return;var ep=data[i];data.RemoveAt(i);data.Insert(j,ep);RefreshEpisodes(j);}
 void MovePages(int delta) {
  if(busy || Current==null || (delta!=-1 && delta!=1))return;
  var chosen=new HashSet<PageItem>(pages.SelectedItems.Cast<PageItem>());
  if(chosen.Count==0)return;
  var visible=pages.Items.Cast<PageItem>().ToList();
  bool changed=false;
  // Swap in the direction of travel so each selected group moves one visible row.
  for(int i=delta<0?1:visible.Count-2;delta<0?i<visible.Count:i>=0;i-=delta) {
   int next=i+delta;
   if(!chosen.Contains(visible[i]) || chosen.Contains(visible[next]))continue;
   var item=visible[i];visible[i]=visible[next];visible[next]=item;changed=true;
  }
  if(!changed)return;
  ApplyPageOrder(visible,chosen,delta);
 }
 void ApplyPageOrder(List<PageItem> visible,HashSet<PageItem> chosen,int delta) {
  int top=pages.TopIndex,horizontal=GetScrollPos(pages.Handle,0);
  // Hidden excluded pages retain their slots and can be restored without losing order.
  int position=0;
  for(int i=0;i<Current.Pages.Count;i++)if(excluded.Checked || !Current.Pages[i].Removed)Current.Pages[i]=visible[position++];
  refreshing=true;pages.BeginUpdate();
  try {
   pages.Items.Clear();pages.Items.AddRange(visible.ToArray());UpdatePageExtent();
   for(int i=0;i<visible.Count;i++)if(chosen.Contains(visible[i]))pages.SetSelected(i,true);
   pages.TopIndex=Math.Min(top,visible.Count-1);
   int edge=delta<0?pages.SelectedIndices[0]:pages.SelectedIndices[pages.SelectedIndices.Count-1];
   if(edge<pages.TopIndex)pages.TopIndex=edge;
   else if(pages.GetItemRectangle(edge).Bottom>pages.ClientSize.Height)pages.TopIndex=Math.Max(0,edge-Math.Max(1,pages.ClientSize.Height/pages.ItemHeight)+1);
  }finally {pages.EndUpdate();RestoreHorizontal(pages,horizontal);refreshing=false;}
  ShowPreview();UpdateStatus();
 }
 void EnablePageDrag() {
  pages.AllowDrop=true;pages.DrawMode=DrawMode.OwnerDrawFixed;pages.ItemHeight=Font.Height+4;
  pages.DrawItem+=delegate(object sender,DrawItemEventArgs e) {
   if(e.Index<0)return;e.DrawBackground();
   TextRenderer.DrawText(e.Graphics,pages.Items[e.Index].ToString(),e.Font,e.Bounds,e.ForeColor,TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.SingleLine|TextFormatFlags.NoPrefix);
   e.DrawFocusRectangle();
   using(var pen=new Pen(Color.DodgerBlue,3)) {
    if(pageInsertAt==e.Index)e.Graphics.DrawLine(pen,e.Bounds.Left,e.Bounds.Top+1,e.Bounds.Right,e.Bounds.Top+1);
    if(pageInsertAt==pages.Items.Count && e.Index==pages.Items.Count-1)e.Graphics.DrawLine(pen,e.Bounds.Left,e.Bounds.Bottom-2,e.Bounds.Right,e.Bounds.Bottom-2);
   }
  };
  pages.MouseDown+=delegate(object sender,MouseEventArgs e) {
   draggedPages=null;pageDragEpisode=null;
   if(busy || e.Button!=MouseButtons.Left || ModifierKeys!=Keys.None)return;
   int i=pages.IndexFromPoint(e.Location);if(i<0)return;
   pageDragStart=e.Location;pageDragEpisode=Current;
   draggedPages=pages.DragSelection.Length>0?pages.DragSelection.Cast<PageItem>().ToList():new List<PageItem>{(PageItem)pages.Items[i]};
  };
  pages.MouseUp+=delegate {draggedPages=null;pageDragEpisode=null;};
  pages.MouseMove+=delegate(object sender,MouseEventArgs e) {
   if(busy || draggedPages==null || e.Button!=MouseButtons.Left || ModifierKeys!=Keys.None)return;
   var box=new Rectangle(pageDragStart.X-SystemInformation.DragSize.Width/2,pageDragStart.Y-SystemInformation.DragSize.Height/2,SystemInformation.DragSize.Width,SystemInformation.DragSize.Height);
   if(box.Contains(e.Location))return;
   refreshing=true;try {for(int i=0;i<pages.Items.Count;i++)pages.SetSelected(i,draggedPages.Contains((PageItem)pages.Items[i]));}finally {refreshing=false;}
   try {pages.DoDragDrop(new DataObject(pageDragFormat,"move"),DragDropEffects.Move);}
   finally {draggedPages=null;pageDragEpisode=null;pageInsertAt=-1;pages.Invalidate();ShowPreview();UpdateStatus();}
  };
  pages.DragEnter+=PageDragOver;pages.DragOver+=PageDragOver;
  pages.DragLeave+=delegate {pageInsertAt=-1;pages.Invalidate();};
  pages.DragDrop+=delegate(object sender,DragEventArgs e) {
   if(busy)return;
   if(e.Data.GetDataPresent(DataFormats.FileDrop)){LoadFiles((string[])e.Data.GetData(DataFormats.FileDrop));return;}
   if(draggedPages!=null && Current==pageDragEpisode && e.Data.GetDataPresent(pageDragFormat))DropPages(PageInsertion(pages.PointToClient(new Point(e.X,e.Y))));
   pageInsertAt=-1;pages.Invalidate();
  };
 }
 void UpdatePageExtent() {pages.HorizontalExtent=pages.Items.Count==0?0:pages.Items.Cast<PageItem>().Max(p=>TextRenderer.MeasureText(p.ToString(),pages.Font,Size.Empty,TextFormatFlags.SingleLine|TextFormatFlags.NoPrefix).Width)+12;}
 int PageInsertion(Point p) {int i=pages.IndexFromPoint(p);if(i<0)return p.Y<0?pages.TopIndex:pages.Items.Count;var rect=pages.GetItemRectangle(i);return p.Y<rect.Top+rect.Height/2?i:i+1;}
 void PageDragOver(object sender,DragEventArgs e) {
  e.Effect=DragDropEffects.None;if(busy)return;
  if(e.Data.GetDataPresent(DataFormats.FileDrop)){e.Effect=DragDropEffects.Copy;return;}
  if(draggedPages==null || Current!=pageDragEpisode || !e.Data.GetDataPresent(pageDragFormat))return;
  e.Effect=DragDropEffects.Move;Point p=pages.PointToClient(new Point(e.X,e.Y));
  if(p.Y<pages.ItemHeight && pages.TopIndex>0)pages.TopIndex--;
  else if(p.Y>pages.ClientSize.Height-pages.ItemHeight && pages.TopIndex<pages.Items.Count-1)pages.TopIndex++;
  pageInsertAt=PageInsertion(p);pages.Invalidate();status.Text="파란 선 위치에 놓으면 선택한 이미지 순서가 바뀝니다.";
 }
 void DropPages(int position) {
  if(busy || Current==null || Current!=pageDragEpisode || draggedPages==null)return;
  var visible=pages.Items.Cast<PageItem>().ToList();var chosen=new HashSet<PageItem>(draggedPages);
  var moving=visible.Where(chosen.Contains).ToList();if(moving.Count==0)return;
  position=Math.Max(0,Math.Min(position,visible.Count));
  int target=visible.Take(position).Count(p=>!chosen.Contains(p));
  visible.RemoveAll(chosen.Contains);visible.InsertRange(target,moving);ApplyPageOrder(visible,chosen,1);
 }
 void RemoveEpisode() {if(busy)return;var chosen=episodes.SelectedItems.Cast<Episode>().ToList();if(chosen.Count==0)return;int i=episodes.SelectedIndex;data.RemoveAll(ep=>chosen.Contains(ep));RefreshEpisodesAtPosition(i,true);}
 void Delete() {var chosen=pages.SelectedItems.Cast<PageItem>().Where(p=>!p.Removed).ToList();if(chosen.Count==0)return;foreach(var p in chosen)p.Removed=true;undo.Push(chosen);RefreshEpisodesAtPosition(episodes.SelectedIndex,true);}
 void Restore() {foreach(var p in pages.SelectedItems.Cast<PageItem>())p.Removed=false;RefreshEpisodesAtPosition(episodes.SelectedIndex,true);}
 void Undo() {if(undo.Count==0)return;foreach(var p in undo.Pop())p.Removed=false;RefreshEpisodesAtPosition(episodes.SelectedIndex,true);}
 void ClearPreview() {var old=preview.Image;preview.Image=null;if(old!=null)old.Dispose();detail.Text="";}
 void ShowPreview() {ClearPreview();var p=pages.SelectedItem as PageItem;if(p==null)return;try {using(var z=ZipFile.OpenRead(p.Archive)) using(var stream=z.Entries[p.Index].Open()) using(var img=ModernImage.Decode(stream,Path.GetExtension(p.Entry))) {preview.Image=new Bitmap(img);detail.Text=p.Entry+"\n"+img.Width+" × "+img.Height+"\n선택"+" "+pages.SelectedItems.Count+"장 · Delete 키로 제외";}} catch {detail.Text=p.Entry+"\n이 이미지의 미리보기를 표시할 수 없습니다. 파일이 정상이라면 원본 그대로 ZIP에 저장됩니다.";}}
 public static void Export(List<Episode> items,string destination) {
  string full=Path.GetFullPath(destination); if(items.SelectMany(e=>e.Pages).Any(p=>string.Equals(p.Archive,full,StringComparison.OrdinalIgnoreCase))) throw new IOException("원본 압축파일과 다른 저장 위치 또는 이름을 선택해 주세요.");
  string temp=full+"."+Guid.NewGuid().ToString("N")+".tmp";
  try {using(var output=ZipFile.Open(temp,ZipArchiveMode.Create)) {int n=0;foreach(var ep in items) {ZipArchive input=null;string active=null;try {foreach(var p in ep.Pages.Where(p=>!p.Removed)) {if(active!=p.Archive) {if(input!=null)input.Dispose();input=ZipFile.OpenRead(p.Archive);active=p.Archive;} var source=input.Entries[p.Index];if(source.FullName!=p.Entry)throw new IOException("원본 파일이 바뀌었습니다. 다시 추가해 주세요.");var entry=output.CreateEntry((++n).ToString("D6")+"_"+Safe(ep.Name)+Path.GetExtension(p.Entry),CompressionLevel.NoCompression);using(var a=source.Open())using(var b=entry.Open())a.CopyTo(b);}} finally {if(input!=null)input.Dispose();}}} if(File.Exists(full))File.Replace(temp,full,null);else File.Move(temp,full); } finally {if(File.Exists(temp))File.Delete(temp);}
 }
 static string Safe(string s) {foreach(char c in Path.GetInvalidFileNameChars())s=s.Replace(c,'_');return s.Length>80?s.Substring(0,80):s;}
 async void Save() {if(data.Sum(e=>e.Pages.Count(p=>!p.Removed))==0) {MessageBox.Show("저장할 이미지를 추가해 주세요.");return;}using(var d=new SaveFileDialog {Filter="ZIP 압축파일|*.zip",FileName="합친 이미지.zip",DefaultExt="zip",AddExtension=true}) {if(d.ShowDialog()!=DialogResult.OK)return;busy=true;Enabled=false;status.Text="ZIP 저장 중입니다. 잠시 기다려 주세요…";try {await Task.Run(()=>Export(data,d.FileName));MessageBox.Show("저장 완료\n"+d.FileName,"이미지 ZIP 합치기");}catch(Exception ex) {MessageBox.Show(ex.Message,"저장 실패");}finally {busy=false;Enabled=true;UpdateStatus();}}}
 [STAThread] static void Main() {Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);Application.Run(new Merger());}
}
