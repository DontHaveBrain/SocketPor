using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Hosting;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Test
{
    public class InitialMEF
    {
        public InitialMEF()
        {
            string drictoryName = "MyDll";
            Directory.CreateDirectory("MyDll");
            Store.dc = new DirectoryCatalog(drictoryName);
            Store.dc.Changed += Dll_CatalogChange;

            Store.fsw = new FileSystemWatcher(drictoryName)//文件夹监视器
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName,
                Filter = "*.dll"
            };
            Store.fsw.Changed += Fsw_Changed;
            Store.fsw.Created += Fsw_Changed;
            Store.fsw.Deleted += Fsw_Changed;
            Store.fsw.Renamed += (sender, e) =>
            {

            };
            Store.fsw.EnableRaisingEvents = true;//文件监控器启动
            Loading();
        }
        void Fsw_Changed(object sender, FileSystemEventArgs e)
        {
            Store.dc?.Refresh();
        }
        void Dll_CatalogChange(object? sender, ComposablePartCatalogChangeEventArgs e)
        {
            Loading();
        }
        void Loading()
        {
            try
            {
                if (Store.compositionContainer == null)
                {
#pragma warning disable CS8604 // 引用类型参数可能为 null。
                    Store.agg = new AggregateCatalog(Store.dc);
#pragma warning restore CS8604 // 引用类型参数可能为 null。

                    Store.compositionContainer = new CompositionContainer(Store.agg);


                    var batch = new CompositionBatch();//添加绑定 ：如果某个接口或实现类依赖一个参数，我们可以这样传进去，当然正确做法是先弄个单例再传
                    CancellationTokenSource cts = new CancellationTokenSource();
                    batch.AddExportedValue(cts);
                    Store.compositionContainer.ComposeParts(batch);
                }
                else
                {
                    Store.compositionContainer.ComposeParts();//重新加载新的dll，并移除文件夹移除的组件
                }
                //展示零件--一般不展示，论文为了大家更好看到效果，这里展示一下 
            }
            catch (Exception ex)
            {
                //这里展示异常信息
                string err = ex.Message;
                Console.WriteLine("错误信息:" + err);
            }
        }
        public static class Store
        {
            public static CompositionContainer? compositionContainer;
            public static DirectoryCatalog? dc;
            public static AggregateCatalog? agg;
            public static FileSystemWatcher? fsw;
        }
    }

}

