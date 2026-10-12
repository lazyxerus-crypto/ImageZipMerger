using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

static class PreviewCleanupTests {
 const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
 static object Field(Merger m,string n){return typeof(Merger).GetField(n,Flags).GetValue(m);}
 static void Set(Merger m,string n,object v){typeof(Merger).GetField(n,Flags).SetValue(m,v);}
 static object Call(Merger m,string n,params object[] args){return typeof(Merger).GetMethod(n,Flags).Invoke(m,args);}
 static void Assert(bool ok,string message){if(!ok)throw new Exception(message);}
 static int UndoCount(Merger m){var u=Field(m,"undo");return (int)u.GetType().GetProperty("Count").GetValue(u,null);}
 static void PumpLayout(Form f){for(int i=0;i<4;i++)f.PerformLayout();}
 static void Settle(Form f){for(int i=0;i<10;i++){f.PerformLayout();Application.DoEvents();}}
 static void Maximize(Form f){for(int i=0;i<50&&(f.WindowState!=FormWindowState.Maximized||f.RestoreBounds.Width<1);i++){try{f.WindowState=FormWindowState.Maximized;}catch{}Application.DoEvents();}}
 static void ExpectClamped(int reqW,int reqH,Form form,Rectangle area,out int ew,out int eh){ExpectClamped(reqW,reqH,form.MinimumSize.Width,form.MinimumSize.Height,area,out ew,out eh);}
 static void ExpectClamped(int reqW,int reqH,int minW,int minH,Rectangle area,out int ew,out int eh){ew=AppSettings.ClampSize(reqW,minW,area.Width);eh=AppSettings.ClampSize(reqH,minH,area.Height);}
 // What a restored form actually shows: WinForms coerces below-minimum sizes up to MinimumSize.
 static void ExpectFormSize(int reqW,int reqH,Form form,Rectangle area,out int ew,out int eh){int cw,ch;ExpectClamped(reqW,reqH,form,area,out cw,out ch);ew=Math.Max(cw,form.MinimumSize.Width);eh=Math.Max(ch,form.MinimumSize.Height);}
 static string MakeImageZip(string path,int w,int h) {
  using(var zip=ZipFile.Open(path,ZipArchiveMode.Create))
   for(int i=0;i<2;i++) using(var s=zip.CreateEntry("img"+i+".png").Open()) using(var b=new Bitmap(w+i,h)) b.Save(s,System.Drawing.Imaging.ImageFormat.Png);
  return path;
 }
 public static void Run(string dir) {
  // 4. default off when file missing
  string missing=Path.Combine(dir,"no-settings.txt");
  Assert(!AppSettings.LoadAutoCleanupFrom(missing),"Auto-cleanup default must be off");
  // 5. persistence roundtrip
  string cfg=Path.Combine(dir,"settings.txt");
  AppSettings.SaveAutoCleanupTo(cfg,true);
  Assert(AppSettings.LoadAutoCleanupFrom(cfg),"Auto-cleanup ON must persist");
  AppSettings.SaveAutoCleanupTo(cfg,false);
  Assert(!AppSettings.LoadAutoCleanupFrom(cfg),"Auto-cleanup OFF must persist");
  // 1,2,3. preview ratio, guidance removal, original ratio preserved
  using(var form=new Merger()){
   form.ShowInTaskbar=false;form.Opacity=0;form.Show();
   var wrap=(Panel)Field(form,"previewWrap");
   var boxContainer=(Panel)Field(form,"previewBox");
   var view=(PictureBox)Field(form,"preview");
   var detail=(Label)Field(form,"detail");
   Assert(wrap!=null && boxContainer!=null && view!=null,"Preview container missing");
   Assert(view.SizeMode==PictureBoxSizeMode.Zoom,"Preview must use contain (Zoom) so nothing is cropped");
   Assert(view.Dock==DockStyle.Fill,"Preview image must fill the ratio container");
   Assert(boxContainer.Dock==DockStyle.None,"Ratio container must not be Dock.Fill or the parent layout will override it");
   Assert(boxContainer.BackColor.ToArgb()!=wrap.BackColor.ToArgb(),"Ratio container must be visually distinct from the surrounding space");
   Assert(wrap.Dock==DockStyle.Fill,"Available-space holder must stay Dock.Fill so horizontal layout is untouched");
   // Detach the available-space holder from the TableLayout so the parent
   // layout engine cannot overwrite the sizes under test (a Dock.Fill child
   // of a TableLayout cell is always resized by the parent).
   var wrapParent=wrap.Parent;
   int wrapCol=0,wrapRow=0;
   if(wrapParent is TableLayoutPanel){wrapCol=((TableLayoutPanel)wrapParent).GetColumn(wrap);wrapRow=((TableLayoutPanel)wrapParent).GetRow(wrap);wrapParent.Controls.Remove(wrap);}
   wrap.Dock=DockStyle.None;
   // Spec examples: full available width kept, height exactly width*1.41.
   foreach(var spec in new[]{new[]{200,400,200,282},new[]{300,500,300,423},new[]{400,700,400,564}}) {
    wrap.Size=new Size(spec[0],spec[1]);Call(form,"LayoutPreview");
    Assert(boxContainer.Width==spec[2] && boxContainer.Height==spec[3],"Spec 1:1.41 failed for avail "+spec[0]+"x"+spec[1]+", got "+boxContainer.Width+"x"+boxContainer.Height+" expected "+spec[2]+"x"+spec[3]);
    Assert(boxContainer.Width==wrap.ClientSize.Width,"Preview width must use the full available width, got "+boxContainer.Width+" in "+wrap.ClientSize.Width);
   }
   // Short space: shrink the preview to fit, never grow the form.
   {
    int formW0=form.Width,formH0=form.Height;
    wrap.Size=new Size(300,120);Call(form,"LayoutPreview");
    Assert(boxContainer.Width==85 && boxContainer.Height==120,"Short space must shrink to fit 85x120, got "+boxContainer.Width+"x"+boxContainer.Height);
    Assert(boxContainer.Width<=wrap.ClientSize.Width && boxContainer.Height<=wrap.ClientSize.Height,"Small preview must stay inside");
    Assert(boxContainer.Left==(300-85)/2 && boxContainer.Top==0,"Small preview must be centered");
    Assert(form.Width==formW0 && form.Height==formH0,"Short space must not resize the form");
    double sratio=(double)boxContainer.Width/(double)boxContainer.Height;
    Assert(Math.Abs(sratio-(1.0/1.41))<0.03,"Small preview must keep 1:1.41");
   }
   foreach(var size in new[]{new Size(500,500),new Size(300,600),new Size(200,400),new Size(150,100)}) {
    int formW0=form.Width,formH0=form.Height;
    wrap.Size=size;Call(form,"LayoutPreview");
    int availW=wrap.ClientSize.Width,availH=wrap.ClientSize.Height;
    Assert(boxContainer.Width<=availW && boxContainer.Height<=availH,"Preview must stay inside, got "+boxContainer.Width+"x"+boxContainer.Height+" in "+availW+"x"+availH);
    Assert(boxContainer.Height>boxContainer.Width,"Preview must be portrait 1:1.41, got "+boxContainer.Width+"x"+boxContainer.Height);
    double ratio=(double)boxContainer.Width/(double)boxContainer.Height;
    Assert(Math.Abs(ratio-(1.0/1.41))<0.03,"Preview container must be 1:1.41, got "+boxContainer.Width+"x"+boxContainer.Height);
    Assert(boxContainer.Left==(availW-boxContainer.Width)/2 && boxContainer.Top==(availH-boxContainer.Height)/2,"Preview must be centered");
    Assert(form.Width==formW0 && form.Height==formH0,"Layout must not resize the form");
    boxContainer.PerformLayout();
    Assert(view.Width==boxContainer.Width && view.Height==boxContainer.Height,"Preview image must fill the ratio container, got "+view.Width+"x"+view.Height+" in "+boxContainer.Width+"x"+boxContainer.Height);
   }
   if(wrapParent is TableLayoutPanel){((TableLayoutPanel)wrapParent).Controls.Add(wrap,wrapCol,wrapRow);wrap.Dock=DockStyle.Fill;wrapParent.PerformLayout();int attachedW=wrap.ClientSize.Width,attachedH=wrap.ClientSize.Height,formW=form.Width,formH=form.Height;Call(form,"LayoutPreview");Assert(wrap.ClientSize.Width==attachedW && wrap.ClientSize.Height==attachedH,"Attached holder must not change");Assert(form.Width==formW && form.Height==formH,"Attached layout must not resize the form");Assert(boxContainer.Width<=attachedW && boxContainer.Height<=attachedH,"Attached preview must fit");Assert(boxContainer.Left==(attachedW-boxContainer.Width)/2 && boxContainer.Top==(attachedH-boxContainer.Height)/2,"Attached preview must be centered");}
   string src=Path.Combine(dir,"prev.zip");MakeImageZip(src,8,4);
   Call(form,"LoadFiles",(object)new[]{src});
   var data=(List<Episode>)Field(form,"data");
   Assert(data.Count==1,"LoadFiles failed");
   Call(form,"RefreshEpisodes",0);
   var pages=(ListBox)Field(form,"pages");
   pages.SelectedIndex=0;Call(form,"ShowPreview");
   Assert(view.Image!=null,"Preview image missing");
   Assert(view.Image.Width==8 && view.Image.Height==4,"Original image ratio must be preserved");
   Call(form,"UpdatePreviewDetail",pages.SelectedItem);
   string text=detail.Text==null?"":detail.Text;
   Assert(!text.Contains("Delete") && !text.Contains("×") && !text.Contains("선택") && !text.Contains(".png") && !text.Contains("해상도"),"Guidance/file-info text must be removed, got '"+text+"'");
   Assert(string.IsNullOrEmpty(text),"Normal preview detail must be empty");
   var box=(CheckBox)Field(form,"autoCleanup");
   Assert(box!=null && box.Text.Contains("자동 정리"),"Auto-cleanup toggle missing");
   var tips=(ToolTip)Field(form,"fileTips");
   Assert(tips.GetToolTip(box).Contains("대기 회차를 비웁니다"),"Auto-cleanup description missing");
   form.Close();
  }
  // 6. toggle OFF keeps old behavior (Export alone never deletes/clears)
  {
   string a=Path.Combine(dir,"keepA.zip"),b=Path.Combine(dir,"keepB.zip");
   MakeImageZip(a,4,4);MakeImageZip(b,4,4);
   using(var form=new Merger()){
    form.ShowInTaskbar=false;form.Opacity=0;form.Show();
    Set(form,"autoCleanupOn",false);
    Call(form,"LoadFiles",(object)new[]{a,b});
    var data=(List<Episode>)Field(form,"data");
    var sources=Merger.CollectSourceArchives(data);
    Assert(sources.Count==2,"Source collection failed");
    string dest=Path.Combine(dir,"keepOut.zip");
    Merger.Export(data,dest);
    Assert(File.Exists(a) && File.Exists(b),"Toggle OFF must not delete sources");
    Assert(data.Count==2,"Toggle OFF must not clear queue");
    Assert(AppSettings.VerifyOutputZip(dest),"Output ZIP must verify");
    form.Close();
   }
  }
  // 7. success cleanup deletes sources and clears queue
  {
   string a=Path.Combine(dir,"cleanA.zip"),c=Path.Combine(dir,"cleanB.zip");
   MakeImageZip(a,4,4);MakeImageZip(c,4,4);
   using(var form=new Merger()){
    form.ShowInTaskbar=false;form.Opacity=0;form.Show();
    Set(form,"autoCleanupOn",true);
    Call(form,"LoadFiles",(object)new[]{a,c});
    var data=(List<Episode>)Field(form,"data");
    var sources=Merger.CollectSourceArchives(data);
    string dest=Path.Combine(dir,"cleanOut.zip");
    Merger.Export(data,dest);
    Assert(AppSettings.VerifyOutputZip(dest),"Output must verify before cleanup");
    bool done=(bool)Call(form,"PerformAutoCleanup",dest,sources);
    Assert(done,"Cleanup must succeed");
    Assert(!File.Exists(a) && !File.Exists(c),"Sources must be deleted");
    Assert(File.Exists(dest),"Destination must survive cleanup");
    Assert(((List<Episode>)Field(form,"data")).Count==0,"Queue must be cleared");
    Assert(UndoCount(form)==0,"Undo must be cleared");
    bool again=(bool)Call(form,"PerformAutoCleanup",dest,sources);
    Assert(!again,"Duplicate cleanup must not run twice");
    form.Close();
   }
  }
  // 8. failure/cancel preserves sources and queue
  {
   string a=Path.Combine(dir,"failA.zip");MakeImageZip(a,4,4);
   string gone=Path.Combine(dir,"gone.zip");MakeImageZip(gone,4,4);
   var missingEp=Merger.ReadArchive(gone);File.Delete(gone);
   Assert(!AppSettings.VerifyOutputZip(Path.Combine(dir,"nope.zip")),"Missing ZIP must not verify");
   Assert(!AppSettings.VerifyOutputZip(gone),"Deleted source must not verify as output");
   using(var form=new Merger()){
    form.ShowInTaskbar=false;form.Opacity=0;form.Show();
    Set(form,"autoCleanupOn",true);
    Call(form,"LoadFiles",(object)new[]{a});
    var data=(List<Episode>)Field(form,"data");
    int before=data.Count;
    var sources=Merger.CollectSourceArchives(data);
    bool done=(bool)Call(form,"PerformAutoCleanup",Path.Combine(dir,"nope.zip"),sources);
    Assert(!done,"Missing output must block cleanup");
    Assert(File.Exists(a),"Source must be preserved on failure");
    Assert(((List<Episode>)Field(form,"data")).Count==before,"Queue must be preserved on failure");
    Assert(AppSettings.IsSamePath(a.ToLowerInvariant(),a.ToUpperInvariant()),"Same-path compare must ignore case");
    // same-path protection
    var sameSources=new List<string>{a};
    bool same=(bool)Call(form,"PerformAutoCleanup",a,sameSources);
    Assert(!same && File.Exists(a),"Same source/destination must block deletion");
    Assert(((List<Episode>)Field(form,"data")).Count==before,"Queue must survive blocked cleanup");
    form.Close();
   }
   // broken export must not touch destination
   string dest=Path.Combine(dir,"failOut.zip");File.Copy(a,dest);
   byte[] beforeBytes=File.ReadAllBytes(dest);
   bool failed=false;try{Merger.Export(new List<Episode>{missingEp},dest);}catch(IOException){failed=true;}
   Assert(failed && beforeBytes.SequenceEqual(File.ReadAllBytes(dest)),"Failed export must preserve destination");
  }
  // 9. only processed files collected; removed-only archives excluded
  {
   string a=Path.Combine(dir,"partA.zip"),d=Path.Combine(dir,"partB.zip");
   MakeImageZip(a,4,4);MakeImageZip(d,4,4);
   var ea=Merger.ReadArchive(a);var eb=Merger.ReadArchive(d);
   foreach(var p in eb.Pages)p.Removed=true;
   var only=Merger.CollectSourceArchives(new List<Episode>{ea,eb});
   Assert(only.Count==1 && AppSettings.IsSamePath(only[0],a),"Only processed archives must be collected");
  }
  // 10. window placement save/restore in the shared settings file
  {
   string pcfg=Path.Combine(dir,"placement.txt");
   AppSettings.SaveWindowPlacementTo(pcfg,1000,700,false);
   var rp=AppSettings.LoadWindowPlacementFrom(pcfg);
   Assert(rp.Width==1000&&rp.Height==700&&!rp.Maximized,"Placement roundtrip failed");
   AppSettings.SaveWindowPlacementTo(pcfg,1000,700,true);
   rp=AppSettings.LoadWindowPlacementFrom(pcfg);
   Assert(rp.Maximized,"Maximized must persist");
   AppSettings.SaveAutoCleanupTo(pcfg,true);
   AppSettings.SaveWindowPlacementTo(pcfg,1000,700,false);
   Assert(AppSettings.LoadAutoCleanupFrom(pcfg),"Window save must preserve AutoCleanup");
   rp=AppSettings.LoadWindowPlacementFrom(pcfg);
   Assert(rp.Width==1000&&!rp.Maximized,"AutoCleanup save must preserve placement");
   File.WriteAllText(pcfg,"garbage\nWidth=abc\n");
   rp=AppSettings.LoadWindowPlacementFrom(pcfg);
   Assert(rp.Width==936&&rp.Height==970&&!rp.Maximized,"Corrupt placement must fall back to defaults");
   Assert(!AppSettings.LoadAutoCleanupFrom(cfg),"Corrupt autocleanup must default off");
   Assert(AppSettings.ClampSize(50,936,1920)==936,"Clamp min failed");
   Assert(AppSettings.ClampSize(5000,936,1920)==1920,"Clamp max failed");
   Assert(AppSettings.ClampSize(1200,936,1920)==1200,"Clamp mid failed");
   {int tw,th;ExpectClamped(936,970,936,620,new Rectangle(0,0,800,600),out tw,out th);Assert(tw==800&&th==600,"Tiny-screen file clamp failed");Assert(Math.Max(tw,936)==936&&Math.Max(th,620)==620,"Tiny-screen form must show minimum size");}
   // No settings file: default size applies (clamped to the working area on
   // small screens) and the preview still fits.
   {
    string nofile=Path.Combine(dir,"no-such-settings.txt");
    if(File.Exists(nofile))File.Delete(nofile);
    string prevCustom=AppSettings.CustomPath;AppSettings.CustomPath=nofile;
    try{
     using(var form=new Merger()){
      form.ShowInTaskbar=false;form.Opacity=0;form.Show();PumpLayout(form);
      int ew,eh;ExpectFormSize(936,970,form,Screen.FromControl(form).WorkingArea,out ew,out eh);
      Assert(form.Width==ew && form.Height==eh,"Default size must apply (area-aware), got "+form.Width+"x"+form.Height+" expected "+ew+"x"+eh);
      var dwrap=(Panel)Field(form,"previewWrap");var dbox=(Panel)Field(form,"previewBox");
      Assert(dbox.Width<=dwrap.ClientSize.Width && dbox.Height<=dwrap.ClientSize.Height,"Default preview must fit without clipping");
      double dratio=(double)dbox.Width/(double)dbox.Height;
      Assert(Math.Abs(dratio-(1.0/1.41))<0.03,"Default preview must keep 1:1.41");
      form.Close();
     }
    }finally{AppSettings.CustomPath=prevCustom;}
   }
   string iso=Path.Combine(dir,"iso-settings.txt");
   AppSettings.SaveWindowPlacementTo(iso,1000,700,false);
   AppSettings.SaveAutoCleanupTo(iso,true);
   string oldCustom=AppSettings.CustomPath;AppSettings.CustomPath=iso;
   try{
    int w1,h1;
    using(var form=new Merger()){
     form.ShowInTaskbar=false;form.Opacity=0;form.Show();PumpLayout(form);
     int ew,eh;ExpectFormSize(1000,700,form,Screen.FromControl(form).WorkingArea,out ew,out eh);
     Assert(form.Width==ew && form.Height==eh,"Restored size must apply (area-aware), got "+form.Width+"x"+form.Height+" expected "+ew+"x"+eh);
     w1=form.Width;h1=form.Height;form.Close();
    }
    rp=AppSettings.LoadWindowPlacementFrom(iso);
    Assert(rp.Width==w1&&rp.Height==h1&&!rp.Maximized,"Close must save last size, got "+rp.Width+"x"+rp.Height);
    Assert(AppSettings.LoadAutoCleanupFrom(iso),"Close save must preserve AutoCleanup");
    int w2,h2;
    using(var form=new Merger()){
     form.ShowInTaskbar=false;form.Opacity=0;form.Show();PumpLayout(form);
     Assert(form.Width==w1&&form.Height==h1,"Saved size must be stable across restarts, got "+form.Width+"x"+form.Height);
     Rectangle rarea=Screen.FromControl(form).WorkingArea;
     int targetW=Math.Max(Math.Min(1100,rarea.Width),form.MinimumSize.Width);
     form.Size=new Size(targetW,h1);PumpLayout(form);
     Assert(form.Width==targetW,"Resize must apply, got "+form.Width+"x"+form.Height+" target="+targetW+" area="+rarea);
     w2=form.Width;h2=form.Height;
     Rectangle area2=Screen.FromControl(form).WorkingArea;Size min2=form.MinimumSize;
     form.Close();
     int ew2,eh2;ExpectClamped(w2,h2,min2.Width,min2.Height,area2,out ew2,out eh2);
     rp=AppSettings.LoadWindowPlacementFrom(iso);
     Assert(rp.Width==ew2&&rp.Height==eh2&&!rp.Maximized,"Resize must be saved (area-aware), got "+rp.Width+"x"+rp.Height+" expected "+ew2+"x"+eh2);
    }
    int bw=rp.Width,bh=rp.Height;
    using(var form=new Merger()){
     form.ShowInTaskbar=false;form.Opacity=0;form.Show();PumpLayout(form);
     Maximize(form);PumpLayout(form);
     form.Close();
    }
    rp=AppSettings.LoadWindowPlacementFrom(iso);
    Assert(rp.Maximized,"Maximized must be saved on close");
    Assert(rp.Width==bw&&rp.Height==bh,"Normal size must be kept when maximized, got "+rp.Width+"x"+rp.Height);
    using(var form=new Merger()){
     // NOTE: do not set ShowInTaskbar here: that recreates the window handle
     // and drops a ctor-restored maximized state (test-only artifact; the
     // real app never changes ShowInTaskbar).
     form.Opacity=0;form.Show();PumpLayout(form);
     for(int i=0;i<50&&form.WindowState!=FormWindowState.Maximized;i++)Application.DoEvents();
     Assert(form.WindowState==FormWindowState.Maximized,"Maximized state must be restored");
     form.Close();
    }
    rp=AppSettings.LoadWindowPlacementFrom(iso);
    Assert(rp.Maximized&&rp.Width==bw&&rp.Height==bh,"Maximized restore must not clobber saved size");
   }finally{AppSettings.CustomPath=oldCustom;}
  }
  Console.WriteLine("PASS: preview ratio/guidance removal/original ratio/settings default+persist/off preserves/success cleanup/failure preserve/partial safety");
 }
}
