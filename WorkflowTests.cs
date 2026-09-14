using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

static class WorkflowTests {
 const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
 [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr h,int message,IntPtr w,IntPtr l);
 static object Field(Merger m,string n){return typeof(Merger).GetField(n,Flags).GetValue(m);}
 static object Call(Merger m,string n,params object[] args){return typeof(Merger).GetMethod(n,Flags).Invoke(m,args);}
 static void Assert(bool ok,string message){if(!ok)throw new Exception(message);}
 static List<byte[]> Contents(string path){using(var z=ZipFile.OpenRead(path))return z.Entries.Select(e=>{using(var s=e.Open())using(var m=new MemoryStream()){s.CopyTo(m);return m.ToArray();}}).ToList();}
 static void NoTemps(string dir){Assert(Directory.GetFiles(dir,"*.tmp").Length==0,"Temporary ZIP leaked");}
 public static void Run(string dir,string source){
  string one=Path.Combine(dir,"chapter1.zip"),two=Path.Combine(dir,"chapter2.zip"),ten=Path.Combine(dir,"chapter10.zip");
  foreach(var path in new[]{one,two,ten})File.Copy(source,path);
  using(var form=new Merger()){
   form.ShowInTaskbar=false;form.Opacity=0;form.Show();
   Assert(form.Icon!=null,"Embedded application icon missing");
   Call(form,"LoadFiles",(object)new[]{ten,two});
   var data=(List<Episode>)Field(form,"data");
   Assert(data.Select(e=>e.Name).SequenceEqual(new[]{"chapter2","chapter10"}),"Batch natural sorting failed");
   Call(form,"LoadFiles",(object)new[]{one});
   Assert(data.Select(e=>e.Name).SequenceEqual(new[]{"chapter2","chapter10","chapter1"}),"Existing order changed on add");
   data[0].Name="renamed";
   Assert((string)typeof(Merger).GetProperty("DefaultSaveName",Flags).GetValue(form,null)=="chapter2.zip","Default name must use first original archive");
   Call(form,"RefreshEpisodes",0);
   var pages=(ListBox)Field(form,"pages");var picture=(PictureBox)Field(form,"preview");
   pages.SelectedIndex=2;var bitmap=picture.Image;var selected=pages.SelectedItem;
   Call(form,"MovePages",1);Assert(ReferenceEquals(bitmap,picture.Image),"Reorder decoded unchanged preview again");
   Call(form,"MoveEpisode",1);Assert(ReferenceEquals(bitmap,picture.Image) && ReferenceEquals(selected,pages.SelectedItem),"Episode move reset page selection/preview");
   // A drag starting on a selected page must retain the whole multi-selection.
   pages.ClearSelected();pages.SetSelected(0,true);pages.SetSelected(2,true);
   var rect=pages.GetItemRectangle(2);IntPtr point=new IntPtr(((rect.Top+2)<<16)|10);
   SendMessage(pages.Handle,0x201,new IntPtr(1),point);
   Assert(pages.SelectedItems.Count==2,"Mouse down collapsed drag multi-selection");
   ((PageListBox)pages).CancelPendingClick();pages.Capture=false;
   Call(form,"Delete");Assert(((Stack<List<PageItem>>)Field(form,"undo")).Count>0,"Delete history setup failed");
   var expected=data.SelectMany(e=>e.Pages.Where(p=>!p.Removed)).Select(p=>Contents(p.Archive)[p.Index]).ToList();
   Assert((bool)Call(form,"IsSourceDestination",two),"Source overwrite not detected");
   var saved=Merger.Export(data,two);NoTemps(dir);
   var actual=Contents(two);Assert(actual.Count==expected.Count && actual.Zip(expected,(a,b)=>a.SequenceEqual(b)).All(v=>v),"Source overwrite bytes/order mismatch");
   Call(form,"AdoptSavedResult",saved);
   Assert(data.Count==1 && data[0]==saved,"Saved output was not adopted as one new task");
   Assert(((Stack<List<PageItem>>)Field(form,"undo")).Count==0,"Old undo references survived overwrite");
   Assert(picture.Image!=null && saved.Pages.All(p=>p.Archive==two && !p.Removed),"Saved preview/references invalid");
   var again=Merger.Export(data,two);Call(form,"AdoptSavedResult",again);NoTemps(dir);
   actual=Contents(two);Assert(actual.Count==expected.Count && actual.Zip(expected,(a,b)=>a.SequenceEqual(b)).All(v=>v),"Second overwrite duplicated or lost images");
   Call(form,"Log","ZIP 저장 중: chapter2.zip",Color.RoyalBlue);Call(form,"Log","저장 완료: chapter2.zip",Color.ForestGreen);
   string screenshot=Environment.GetEnvironmentVariable("IMAGEZIPMERGER_TEST_PREVIEW");
   if(!string.IsNullOrEmpty(screenshot))using(var image=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(image,new Rectangle(0,0,form.Width,form.Height));image.Save(screenshot);}
   for(int i=0;i<150;i++)Call(form,"Log",new string('L',3000)+i,Color.ForestGreen);
   var log=(ListBox)Field(form,"log");Assert(log.Items.Count==100 && log.Items.Cast<object>().All(x=>x.ToString().Length<1600),"Log memory bounds failed");
   form.Close();
  }
  // Errors must leave an existing destination intact and clean the partial output.
  string broken=Path.Combine(dir,"missing.zip"),destination=Path.Combine(dir,"protected.zip");File.Copy(source,broken);File.Copy(source,destination);
  var missing=Merger.ReadArchive(broken);File.Delete(broken);byte[] before=File.ReadAllBytes(destination);
  bool failed=false;try {Merger.Export(new List<Episode>{missing},destination);}catch(IOException){failed=true;}
  Assert(failed && before.SequenceEqual(File.ReadAllBytes(destination)),"Failed export changed destination");NoTemps(dir);
  failed=false;try {Merger.Export(new List<Episode>(),destination);}catch(IOException){failed=true;}
  Assert(failed && before.SequenceEqual(File.ReadAllBytes(destination)),"Empty export replaced destination");NoTemps(dir);
  var wrong=Merger.ReadArchive(source);wrong.Pages[0].Entry="changed.png";
  failed=false;try {Merger.Export(new List<Episode>{wrong},destination);}catch(IOException){failed=true;}
  Assert(failed && before.SequenceEqual(File.ReadAllBytes(destination)),"Changed source replaced destination");NoTemps(dir);
  failed=false;using(var locked=new FileStream(destination,FileMode.Open,FileAccess.Read,FileShare.Read)){
   try {Merger.Export(new List<Episode>{Merger.ReadArchive(source)},destination);}catch(IOException){failed=true;}
  }
  Assert(failed && before.SequenceEqual(File.ReadAllBytes(destination)),"Locked destination was modified");NoTemps(dir);
  Console.WriteLine("PASS: batch sorting/default name/preview reuse/native drag selection/source overwrite/reload/repeat save/bounded logs/failure cleanup/icon");
 }
}
