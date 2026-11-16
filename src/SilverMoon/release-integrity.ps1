# Read LABEL_SECURITY_INFORMATION explicitly: Get-Acl's default DACL view omits it.
if (-not ('Middleton.ReleaseLabels' -as [type])) {
Add-Type -TypeDefinition @'
using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
namespace Middleton {
 public static class ReleaseLabels {
  [DllImport("advapi32.dll",CharSet=CharSet.Unicode)] static extern uint GetNamedSecurityInfo(string name,int kind,uint info,out IntPtr owner,out IntPtr group,out IntPtr dacl,out IntPtr sacl,out IntPtr descriptor);
  [DllImport("advapi32.dll",CharSet=CharSet.Unicode,SetLastError=true)] static extern bool ConvertSecurityDescriptorToStringSecurityDescriptor(IntPtr descriptor,uint revision,uint info,out IntPtr text,out uint length);
  [DllImport("kernel32.dll")] static extern IntPtr LocalFree(IntPtr memory);
  public static string Read(string path) {
   IntPtr owner,group,dacl,sacl,sd,text=IntPtr.Zero;uint length;
   uint result=GetNamedSecurityInfo(path,1,0x10,out owner,out group,out dacl,out sacl,out sd);
   if(result!=0)throw new Win32Exception((int)result);
   try {
    if(!ConvertSecurityDescriptorToStringSecurityDescriptor(sd,1,0x10,out text,out length))throw new Win32Exception(Marshal.GetLastWin32Error());
    return Marshal.PtrToStringUni(text) ?? "";
   } finally {if(text!=IntPtr.Zero)LocalFree(text);LocalFree(sd);}
  }
 }
}
'@
}
function Assert-ReleaseIntegrity([string]$Path) {
    $label=[Middleton.ReleaseLabels]::Read($Path)
    foreach($match in [regex]::Matches($label,'\(ML;[^)]*;;;([^;)]+)\)')) {
        $sid=$match.Groups[1].Value
        if($sid -in @('LW','UN') -or ($sid -match '^S-1-16-(\d+)$' -and [long]$Matches[1] -lt 8192)) {
            throw "Low/untrusted integrity label blocks release: $Path ($label). Current release was not replaced."
        }
    }
}
