using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

// Handle empty-space gestures before the native list box selects its last row.
class EpisodeListBox : ListBox {
 public bool SelectingArea {get;private set;}
 public event EventHandler AreaSelectionFinished;
 Point origin; Rectangle area; int startTop; bool[] initial; bool additive;
 protected override void WndProc(ref Message m) {
  int code=m.Msg;
  if(code==0x201) {
   Point p=new Point(unchecked((short)((long)m.LParam&65535)),unchecked((short)(((long)m.LParam>>16)&65535)));
   if(ClientRectangle.Contains(p) && IndexFromPoint(p)<0) {
    Focus();SelectingArea=true;origin=p;startTop=TopIndex;area=Rectangle.Empty;
    additive=(ModifierKeys&Keys.Control)!=0;initial=Enumerable.Range(0,Items.Count).Select(GetSelected).ToArray();
    if(!additive)ClearSelected();Capture=true;return;
   }
  }
  if(SelectingArea && code==0x200) {
   Point p=PointToClient(Cursor.Position);UpdateArea(p);return;
  }
  if(SelectingArea && (code==0x202 || code==0x215 || (code==0x100 && (int)m.WParam==27))) {
   if(code==0x100)for(int i=0;i<Items.Count && i<initial.Length;i++)SetSelected(i,initial[i]);
   SelectingArea=false;Capture=false;area=Rectangle.Empty;Invalidate();
   if(AreaSelectionFinished!=null)AreaSelectionFinished(this,EventArgs.Empty);return;
  }
  base.WndProc(ref m);
  if(code==0xF && SelectingArea && !area.IsEmpty)using(var g=Graphics.FromHwnd(Handle))using(var pen=new Pen(Color.DodgerBlue))using(var brush=new SolidBrush(Color.FromArgb(35,Color.DodgerBlue))) {g.FillRectangle(brush,area);g.DrawRectangle(pen,area);}
 }
 void UpdateArea(Point p) {
  if(p.Y<0 && TopIndex>0)TopIndex--;
  else if(p.Y>ClientSize.Height && TopIndex<Items.Count-1)TopIndex++;
  int anchor=origin.Y+(startTop-TopIndex)*ItemHeight;
  area=Rectangle.FromLTRB(Math.Min(origin.X,p.X),Math.Min(anchor,p.Y),Math.Max(origin.X,p.X)+1,Math.Max(anchor,p.Y)+1);
  BeginUpdate();
  try {for(int i=0;i<Items.Count;i++)SetSelected(i,(additive && i<initial.Length && initial[i]) || area.IntersectsWith(GetItemRectangle(i)));}
  finally {EndUpdate();}Invalidate();
 }
}
class PageListBox : System.Windows.Forms.ListBox {
 public object[] DragSelection = new object[0];
 bool pendingClick;Message down;
 public void CancelPendingClick(){pendingClick=false;}
 protected override void WndProc(ref System.Windows.Forms.Message m) {
  if(m.Msg==0x201) {
   var p=new System.Drawing.Point(unchecked((short)((long)m.LParam&65535)),unchecked((short)(((long)m.LParam>>16)&65535)));
   int i=IndexFromPoint(p);DragSelection=i>=0 && GetSelected(i)?SelectedItems.Cast<object>().ToArray():new object[0];
   if(i>=0 && GetSelected(i) && ModifierKeys==Keys.None){Focus();pendingClick=true;down=m;Capture=true;OnMouseDown(new MouseEventArgs(MouseButtons.Left,1,p.X,p.Y,0));return;}
  }
  if(m.Msg==0x202 && pendingClick){pendingClick=false;base.WndProc(ref down);}
  if(m.Msg==0x215)pendingClick=false;
  base.WndProc(ref m);
 }
}
