using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
class ReorderTests {
 const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
 [DllImport("user32.dll")] static extern int GetScrollPos(IntPtr h,int bar);
 [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr h,int msg,IntPtr w,IntPtr l);
 static object Field(Merger m,string n){return typeof(Merger).GetField(n,Flags).GetValue(m);}
 static void Set(Merger m,string n,object v){typeof(Merger).GetField(n,Flags).SetValue(m,v);}
 static void Call(Merger m,string n,params object[] args){typeof(Merger).GetMethod(n,Flags).Invoke(m,args);}
 static void Assert(bool ok,string message){if(!ok)throw new Exception(message);}
 static void Select(ListBox list,params int[] indices){list.ClearSelected();foreach(int i in indices)list.SetSelected(i,true);}
 static void Order(Episode ep,params PageItem[] expected){Assert(ep.Pages.SequenceEqual(expected),"Unexpected page order");}
 static int ScrollRight(ListBox list){SendMessage(list.Handle,0x114,new IntPtr(7),IntPtr.Zero);int x=GetScrollPos(list.Handle,0);Assert(x>0,"Horizontal scrollbar must be scrollable");return x;}
 [STAThread] static void Main(){
  string dir=Path.Combine(Path.GetTempPath(),"ImageZipMerger-tests-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dir);
  try {
   string source=Path.Combine(dir,"source.zip");
   using(var zip=ZipFile.Open(source,ZipArchiveMode.Create))for(int i=0;i<5;i++)using(var stream=zip.CreateEntry(new string('x',100)+i+".png").Open())using(var bitmap=new Bitmap(2+i,2)){bitmap.Save(stream,System.Drawing.Imaging.ImageFormat.Png);}
   byte[] original=File.ReadAllBytes(source);
   using(var form=new Merger()){
    form.ShowInTaskbar=false;form.Opacity=0;form.Show();
    var data=(List<Episode>)Field(form,"data");var ep=Merger.ReadArchive(source);ep.Name=new string('E',180);data.Add(ep);data.Add(new Episode{Name=new string('F',180)});Call(form,"RefreshEpisodes",0);
    var pages=(ListBox)Field(form,"pages");var episodes=(ListBox)Field(form,"episodes");var p=ep.Pages.ToArray();
    Select(pages,1,2);int horizontal=ScrollRight(pages);Call(form,"MovePages",1);Order(ep,p[0],p[3],p[1],p[2],p[4]);Assert(pages.SelectedItems.Count==2,"Multi-selection lost");Assert(GetScrollPos(pages.Handle,0)==horizontal,"Page horizontal scroll lost on move");
    Call(form,"MovePages",-1);Order(ep,p);Select(pages,0);Call(form,"MovePages",-1);Order(ep,p);
    Select(pages,1,3);Call(form,"MovePages",-1);Order(ep,p[1],p[0],p[3],p[2],p[4]);
    ep.Pages=p.ToList();p[1].Removed=true;Call(form,"RefreshPages");Select(pages,1);Call(form,"MovePages",-1);Order(ep,p[2],p[1],p[0],p[3],p[4]);
    Set(form,"draggedPages",new List<PageItem>{p[2],p[3]});Set(form,"pageDragEpisode",ep);horizontal=ScrollRight(pages);Call(form,"DropPages",4);Order(ep,p[0],p[1],p[4],p[2],p[3]);Assert(GetScrollPos(pages.Handle,0)==horizontal,"Page horizontal scroll lost on drag");Assert(pages.SelectedItems.Count==2,"Drag selection lost");
    Call(form,"DropPages",0);Order(ep,p[2],p[1],p[3],p[0],p[4]);
    ((CheckBox)Field(form,"excluded")).Checked=true;Select(pages,1);Call(form,"MovePages",1);Order(ep,p[2],p[3],p[1],p[0],p[4]);Call(form,"Restore");Assert(!p[1].Removed,"Restore failed");
    horizontal=ScrollRight(pages);Call(form,"Delete");Assert(GetScrollPos(pages.Handle,0)==horizontal,"Page horizontal scroll lost on delete");Call(form,"Undo");Assert(GetScrollPos(pages.Handle,0)==horizontal,"Page horizontal scroll lost on undo");
    int epHorizontal=ScrollRight(episodes);Call(form,"MoveEpisode",1);Assert(GetScrollPos(episodes.Handle,0)==epHorizontal,"Episode horizontal scroll lost on move");Call(form,"DropEpisode",ep,0);Assert(GetScrollPos(episodes.Handle,0)==epHorizontal,"Episode horizontal scroll lost on drag");
    string output=Path.Combine(dir,"output.zip");Merger.Export(data,output);
    using(var a=ZipFile.OpenRead(source))using(var b=ZipFile.OpenRead(output)){
     Assert(b.Entries.Count==5,"Export count");for(int i=0;i<5;i++)using(var x=a.Entries[ep.Pages[i].Index].Open())using(var y=b.Entries[i].Open())using(var bx=new MemoryStream())using(var by=new MemoryStream()){x.CopyTo(bx);y.CopyTo(by);Assert(bx.ToArray().SequenceEqual(by.ToArray()),"Export bytes/order mismatch");}
    }
    Assert(original.SequenceEqual(File.ReadAllBytes(source)),"Source archive modified");
    form.Close();
   }
   WorkflowTests.Run(dir,source);
   Console.WriteLine("PASS: button/multi-selection/boundaries/hidden excluded/drag/restore/scroll/export/source preservation");
  }finally{Directory.Delete(dir,true);}
 }
}
