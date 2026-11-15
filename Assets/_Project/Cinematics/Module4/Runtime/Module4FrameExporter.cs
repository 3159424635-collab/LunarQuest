using System;
using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;
using Process = System.Diagnostics.Process;
using ProcessStartInfo = System.Diagnostics.ProcessStartInfo;

namespace LunarQuest.Module4
{
    [DefaultExecutionOrder(-200)]
    public sealed class Module4FrameExporter : MonoBehaviour
    {
        Module4ShowcaseController controller;
        Process encoder;
        readonly StringBuilder errors = new StringBuilder();
        bool recording;
        bool stills;
        int frame;
        int warmup = 20;
        int stillIndex;
        int stillWarmup;
        string destination;
        string encoderPath;
        string audioPath;
        int fps = 30;
        readonly float[] times = {3,15,32,50,74,80,90,108,118,135,154,171,180,189};

        static string Argument(string key)
        {
            var args = Environment.GetCommandLineArgs();
            for (int i=0;i<args.Length-1;i++) if(args[i]==key)return args[i+1];
            return null;
        }

        void Awake()
        {
            destination = Argument("-m4record");
            string stillFolder = Argument("-m4stills");
            recording = !string.IsNullOrEmpty(destination);
            stills = !string.IsNullOrEmpty(stillFolder);
            if (!recording && !stills) { enabled=false; return; }
            if (stills) destination = stillFolder;
            encoderPath = Argument("-m4ffmpeg");
            audioPath = Argument("-m4audio");
            controller = GetComponent<Module4ShowcaseController>();
            controller.externalClock = true;
            controller.exportMode = true;
            QualitySettings.vSyncCount=0;
            Application.targetFrameRate=-1;
            Time.captureFramerate=fps;
            Screen.SetResolution(1280,720,false);
            if(stills)Directory.CreateDirectory(destination);
            StartCoroutine(Export());
        }

        void Update()
        {
            if(warmup>0) { warmup--; controller.forcedTime=0; return; }
            controller.forcedTime = stills ? times[Mathf.Min(stillIndex,times.Length-1)] : (double)frame/fps;
        }

        IEnumerator Export()
        {
            while (true)
            {
                yield return new WaitForEndOfFrame();
                if (warmup>0) continue;
                if (stills && ++stillWarmup<16) continue;
                Texture2D texture=ScreenCapture.CaptureScreenshotAsTexture();
                if(stills)
                {
                    File.WriteAllBytes(Path.Combine(destination,"M4_"+times[stillIndex].ToString("000")+".png"),texture.EncodeToPNG());
                    Destroy(texture);
                    stillIndex++; stillWarmup=0;
                    if(stillIndex==times.Length){Application.Quit(0);yield break;}
                    continue;
                }
                if(encoder==null)StartEncoder(texture);
                byte[] bytes=texture.GetRawTextureData();
                encoder.StandardInput.BaseStream.Write(bytes,0,bytes.Length);
                Destroy(texture);
                frame++;
                if(frame%300==0)Debug.Log("M4_CAPTURE_FRAME:"+frame);
                if(frame >= Mathf.RoundToInt(controller.totalDuration*fps))
                {
                    encoder.StandardInput.Close();
                    encoder.WaitForExit();
                    File.WriteAllText(destination+".encoder.log",errors.ToString());
                    File.WriteAllText(destination+".capture.json","{\"frames\":"+frame+",\"fps\":"+fps+",\"encoderExit\":"+encoder.ExitCode+",\"nativeCompletedSignals\":"+controller.completedSignals+"}");
                    Application.Quit(encoder.ExitCode);
                    yield break;
                }
            }
        }

        void StartEncoder(Texture2D texture)
        {
            string pixel=texture.format==TextureFormat.RGB24?"rgb24":"rgba";
            var info=new ProcessStartInfo {
                FileName=encoderPath,
                Arguments="-y -hide_banner -f rawvideo -pixel_format "+pixel+" -video_size "+texture.width+"x"+texture.height+" -framerate "+fps+" -i pipe:0 -i \""+audioPath+"\" -vf vflip -c:v libx264 -preset fast -crf 20 -pix_fmt yuv420p -c:a aac -b:a 192k -shortest -movflags +faststart \""+destination+"\"",
                UseShellExecute=false,CreateNoWindow=true,
                RedirectStandardInput=true,RedirectStandardError=true,
                WindowStyle=System.Diagnostics.ProcessWindowStyle.Hidden
            };
            encoder=new Process{StartInfo=info};
            encoder.ErrorDataReceived+=(sender,args)=>{if(args.Data!=null)lock(errors)errors.AppendLine(args.Data);};
            encoder.Start(); encoder.BeginErrorReadLine();
        }

        void OnDestroy()
        {
            if(encoder!=null && !encoder.HasExited) { try{encoder.StandardInput.Close();}catch{} }
        }
    }
}
