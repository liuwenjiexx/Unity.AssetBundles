# Unity.AssetBundles



﻿####菜单

###### Build/AssetBundles

- **Build**

  生成 AssetBundle

- **Edit Config**

  编辑配置，为资源文件生成 AssetBundleName 和 Variant

- **Open Output Path**

  打开生成AssetBundle位置，在配置OutputPath中指定

- **Update All AssetBundle Names**

  为 Assets 目录下所有资源文件重新生成AssetBundleName，在Unity编辑器首次启动时也会自动执行一次。

- **Analysis**

  运行时查看 AssetBundle 加载状态，优化资源加载和卸载

- **gen AssetBundleNames**

  查找所有带AssetBundleName的资源文件，生成资源名称引用类 AssetBundlesNames.dll。将配置BuildGenerateAssetBundleNamesClass设置为true，Build AssetBundle时会自动生成。

- - [x] **Editor Asset Mode**

  默认勾选，只在编辑器环境中生效，选中时，直接加载项目下对应 AssetBundleName 的资源，可提高开发效率。不勾选时，和真机运行时环境一样，真实加载AssetBundle，可以验证生成的资源包是否正确和优化资源加载

  



#### 配置文件

**ProjectSettings/AssetBundleConfig.json**

自动完成对资源的 AssetBundleName 和 Variant 设置



####配置文件格式

**AssetBundleConfig.json**

```
{
	"OutputPath":"AssetBundles/{$Platform}",
	"BuildCopyTo": "Assets/StreamingAssets/AssetBundles/{$Platform}",
	"AssetBundleName": "{$Directory}",
  	"AssetBundleNamesClassFilePath": "Assets/Plugins/gen/AssetBundleNames.dll",
	"AssetBundleNamesClassName": "{$Directory}",
  	"BuildGenerateAssetBundleNamesClass": true,
  	"IgnorePaths": [ "/Assets/StreamingAssets/" ],
  	"Items": [
   	{
    	"Directory": "Assets/AssetBundles",
     	"AssetBundleName": "{$Directory}",
     	"AssetClass": "{$Directory}"
    }]
}
```



**OutputPath**

  	AssetBundle 文件生成目录

**BuildCopyTo**

​	生成AssetBundle 时同时从OutputPath复制到该目录

**AssetBundleName**

​	默认的 AssetBundle 名称

**IgnorePaths**

​	忽略的目录

**AssetBundleNamesClassFilePath**

​	生成 AssetBundleNames.dll 位置

**BuildGenerateAssetBundleNamesClass**

​	Build 时生成 AssetBundleNames.dll

**AssetBundleNamesClassName**

​	默认 AssetName 类名

**Items**

**Directory**

​	包含的资源的目录

**Pattern**

​	正则表达式，用于筛选 Directory 下的文件

**AssetBundleName**

​	AssetBundle 名称

**AssetClass**

​	AssetName 类名

**AssetName**

​	资源名称，对应生成AssetBundle时的 AddressableName，默认为 AssetPath，可重写

**Preloaded**

​	是否为预加载资源

**Ignore**

​	是否忽略该目录

**Variants**

​	资源的 Variant 名称

**Tag**

​	给资源添加标签，多个标签使用 "," 隔开，在 AssetBundleNames/AssetBundleAttribute.Tags 获取，扩展自定义逻辑处理





#### 参数变量格式

```
${variable:format}
```

全局 variable

- **BuildTarget**

  EditorUserBuildSettings.activeBuildTarget

- **Platform**

  编辑器平台名 BuildTarget 对应运行时的 RuntimePlatform

  比如：Windows,Android,iOS

AssetPath 支持的 variable

  样例 Assets/Dir/file1_en.png

- **AssetPath**

  返回完整 AssetPath

  结果：Assets/Dir/file1_en.png

- **Directory**

  返回目录名

  结果：Dir

- **AssetName**

  返回文件名不包含扩展名

  结果: file1_en

- **FileName**

  返回文件名

  结果: file1_en.png

- **FileExtension**

  返回文件扩展名

  结果: .png

**format**

- **/regex/**

  使用正则表达式提取字符串，提取内容为第一个匹配组

  {$AssetName:/(.*)_en/} 

  结果: file1



#### **[AssetBundle Info]** 预览面板

快速验证 **AssetBundleConfig.json** 配置是否正确, 选中某个资源，在 **[Inspector]** 下方预览窗口标题选中 **[AssetBundle Info]**

- **Asset Path**

  当前资源的真实位置

- **Asset Name**

  生成 AssetBundle 时的资源名称

- **Asset Type**

  资源类型

- **Preloaded**

  是否为预加载资源





### 代码说明

```c#
Task<AssetBundleManifest> DownloadManifestAsync(string manifestUrl)
```

下载资源清单文件

**manifestUrl**

​	清单文件(AssetBundleManifest) url 地址



```C#
Task<Object> LoadAssetAsync(string assetBundleName, string assetName, Type type = null, object owner = null)
```

加载 assetName 资源，如果资源的 AssetBundle 未加载则加载 assetBundleName AssetBundle 再加载资源

**assetBundleName**

​	AssetBundle 名称，小写

**assetName**

​	资源名称，小写（AssetPath、FileName、AssetName文件名不包含扩展名) 

**type**

​	资源类型

**owner**

​	AssetBundle 生存期管理对象，如果内存中没有对 owner 的引用则可以调用 UnloadUnused 来释放已加载的AssetBundle，在 AssetBundle Analysis 窗口可以查看 owner 的引用状态



```c#
void UnloadUnused(IEnumerable<string> assetBundleNames, bool allObjects = false)
```

释放未使用的AssetBundle，未使用的判断依据是检查是否存在引用 Load 时传递的 Owner 参数

**assetBundleNames**

​	释放指定的 AssetBundle

**allObjects** 

​	将传递给 AssetBundle.Unload(allObjects)