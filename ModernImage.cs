using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;

static class ModernImage {
 const string Dll="libvips-42.dll";
 [DllImport(Dll,CallingConvention=CallingConvention.Cdecl)] static extern int vips_init(string name);
 [DllImport(Dll,CallingConvention=CallingConvention.Cdecl)] static extern void vips_cache_set_max(int count);
 [DllImport(Dll,CallingConvention=CallingConvention.Cdecl)] static extern IntPtr vips_image_new_from_buffer(IntPtr bytes,UIntPtr size,string options,IntPtr end);
 [DllImport(Dll,CallingConvention=CallingConvention.Cdecl)] static extern int vips_image_write_to_buffer(IntPtr img,string suffix,out IntPtr bytes,out UIntPtr size,IntPtr end);
 [DllImport(Dll,CallingConvention=CallingConvention.Cdecl)] static extern void g_object_unref(IntPtr obj);
 [DllImport(Dll,CallingConvention=CallingConvention.Cdecl)] static extern void g_free(IntPtr obj);
 [DllImport(Dll,CallingConvention=CallingConvention.Cdecl)] static extern void vips_error_clear();
 static bool ready;
 public static Bitmap Decode(Stream stream,string extension) {
  if(extension.ToLowerInvariant()!=".webp" && extension.ToLowerInvariant()!=".avif") {
   using(var img=Image.FromStream(stream))return new Bitmap(img);
  }
  if(!ready) {if(vips_init("ImageZipMerger")!=0)throw new IOException("이미지 디코더 초기화 실패");vips_cache_set_max(0);ready=true;}
  byte[] data;using(var memory=new MemoryStream()) {stream.CopyTo(memory);data=memory.ToArray();}
  IntPtr input=Marshal.AllocHGlobal(data.Length),imgPtr=IntPtr.Zero,output=IntPtr.Zero;
  try {
   Marshal.Copy(data,0,input,data.Length);
   imgPtr=vips_image_new_from_buffer(input,(UIntPtr)(uint)data.Length,"",IntPtr.Zero);
   if(imgPtr==IntPtr.Zero)throw new IOException("이미지 데이터가 손상되었거나 지원되지 않습니다.");
   UIntPtr length;
   if(vips_image_write_to_buffer(imgPtr,".png",out output,out length,IntPtr.Zero)!=0)throw new IOException("미리보기 변환에 실패했습니다.");
   byte[] png=new byte[checked((int)length.ToUInt64())];Marshal.Copy(output,png,0,png.Length);
   using(var memory=new MemoryStream(png))using(var img=Image.FromStream(memory))return new Bitmap(img);
  } finally {if(output!=IntPtr.Zero)g_free(output);if(imgPtr!=IntPtr.Zero)g_object_unref(imgPtr);Marshal.FreeHGlobal(input);vips_error_clear();}
 }
}
