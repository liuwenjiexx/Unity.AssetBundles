# Unity.AssetBundles



## 预览


![Settings](Doc\settings.png)



![Group](Doc\group.PNG)



## 菜单

菜单位置 Build/AssetBundles

- **Build**

  生成 AssetBundle

- **Settings**

  编辑配置，为AssetBundle生成 AssetBundleName 和 Variant

- **运行模式**

  - **Editor Mode**

    默认，只在编辑器环境中生效，直接加载 AssetBundleName 的资源，不用预先生成资源包

  - **Build Mode**

    只在编辑器环境中生效，需要生成资源包，从生成`输出目录`中加载AssetBundle，可以在编辑器中验证生成的资源包是否正确

  - **Download Mode**

    和真机运行时环境一样，如果有下载地址检查版本更新，检查真机运行时资源是否正确

- **Analysis**

  运行时查看 AssetBundle 加载状态，资源引用信息，优化资源加载和卸载

  



## 配置

1. 点击菜单 `Build/AssetBundles/Settings` 打开配置面板

   配置文件生成位置 `<Project>/PojectSettings/AssetBundle.json`

2. 勾选 `开启` 开启该模块

3. 配置 AssetBundle 规则，自动完成对资源的 AssetBundleName 和 Variant 设置

   - 资源包名称

     默认为文件夹名：`{$AssetPath:#DirectoryName}`

   - 资源名称

     默认为 Assets 路径，`{$AssetPath}`

4. 设置排除路径 `排除目录`

   默认

   - `/Assets/StreamingAssets/*`
   - `*/Resources/*`
   
   



- 更新所有 Bundle 名称

  为 Assets 目录下所有资源文件重新生成AssetBundleName，在Unity编辑器首次启动时也会自动执行一次。

- 移除未使用的 Bundle 名称

  移除未使用的AssetBundle 名称

- 生成 AssetBundleNames.dll

  查找所有带AssetBundleName的资源文件，生成资源名称引用类 AssetBundlesNames.dll。勾选AssetBundleNames类，构建 AssetBundle 时会自动生成。



**目录**

- 输出

  打开构建输出目录，配置属性 `构建路径`

- 本地

  打开下载的本地目录

- StreamingAssets

  打开 StreamingAssets AssetBundle 目录，配置属性 StreamingAssetsPath





### 参数格式

```
{$variable:format}
```

全局 variable

- **BuildTarget**

  EditorUserBuildSettings.activeBuildTarget

- **Platform**

  编辑器平台名 BuildTarget 对应运行时的 RuntimePlatform

  比如：Windows, Android, iOS

- **AssetPath**

  返回完整 AssetPath

**详细说明**

​	[String Format](../System.StringFormat/README.md)



#### 常用格式

```
{$AssetPath:#DirectoryPath}
```

  文件夹路径，`Assets/...`

```
{$AssetPath:#DirectoryPath,-1}
```

  文件夹路径，`...`

```
{$AssetPath:#DirectoryName}
```

  文件夹名

```
{$AssetPath:#DirectoryPath}/{$AssetPath:#FileNameWithoutExtension}
```

  文件夹路径/文件名(不带扩展名)



### 配置 link.xml

```xml
<assembly fullname="UnityEngine.Coroutines">
	<type fullname="UnityEngine.Coroutines.UnityCoroutineScheduler" preserve="all"/>
</assembly>
```




## 初始化

```c#
//设置 RuntimePlatform
#if UNITY_ANDROID
	AssetBundles.Platform = RuntimePlatform.Android;
#elif UNITY_IOS
	AssetBundles.Platform = RuntimePlatform.IPhonePlayer;
#endif

//变体
//AssetBundles.Variants.Add("<变体>");

//初始化
yield return AssetBundles.InitializeAsync(); 
```



## 使用

### 初始化

```c#
Task<string> InitializeAsync()
```

根据 AssetBundleMode 初始化，初始化完成后通过 AssetBundles.ManifestUrl 获取当前资源地址



#### 初始化流程

1. 下载 `根版本文件` 获得最新版本号

   如果设置了`AssetBundleSettings.DownloadUrl` 则开始下载，否则跳过下载

2. 根据版本号下载远程清单到本地

3. 加载本地清单文件

   本地清单位于 Application.persistentDataPath/AssetBundleSettings.LocalPath

4. 预加载

5. 初始化完成

    



### 加载

```c#
T LoadAsset<T>(string assetBundleName, string assetName, object owner)
Task<T> LoadAssetAsync<T>(string assetBundleName, string assetName)
```
- **assetBundleName**

​	AssetBundle 名称，小写

- **assetName**

​	资源名称，支持（路径、文件名、文件名不含扩展名) 

- **owner**

  AssetBundle 生存期管理对象，如果内存中没有对 owner 的引用则可以调用 UnloadUnused 来释放已加载的AssetBundle，在 AssetBundle Analysis 窗口可以查看 owner 的引用状态
  
- **T**

​	资源类型
加载 assetName 资源，如果资源的 AssetBundle 未加载则加载 assetBundleName AssetBundle 再加载资源

**样例**

```c#
AssetBundles.LoadAsset<GameObject>("<BundleName>", "<AssetName>");

//使用AssetBundleNames 访问
AssetBundles.LoadAsset<GameObject>(AssetBundleNames.<BundleName>.<AssetName>);
AssetBundles.LoadAsset<GameObject>(AssetBundleNames.<BundleName>_AssetBundle, "<AssetName>");
```



异步加载

```c#
//使用协程方式
var prefabTask = AssetBundles.LoadAssetAsync<GameObject>("<BundleName>", "<AssetName>");
yield return prefabTask;
//获取返回值：prefabTask.Result

//使用回调方式
AssetBundles.LoadAssetAsync<GameObject>("<BundleName>", "<AssetName>")
	.ContinueWith(t =>
	{
		//获取返回值：t.Result
	});
```



#### 定制资源加载类

```c#
Assets.Prefab.Effect.Load<GameObject>("<AssetName>");
Assets.Prefab.Character.Instantiate("<AssetName>");
```



##### 定义资源接口

已定义: UnityEngine.Game.IAssetLoader

```c#
public interface IAssetLoader
{
	UnityEngine.Object Load(string assetName, Type assetType);
}
```

##### 实现资源接口

AssetBundles 加载

```c#
class AssetBundlesLoader : IAssetLoader
{
    public string assetNamePrefix;

    public AssetBundlesLoader(string assetNamePrefix)
    {
    	this.assetNamePrefix = assetNamePrefix ?? string.Empty;
    }

    public UnityEngine.Object Load(string assetName, Type assetType)
    {
	    return AssetBundles.LoadAsset(assetNamePrefix + assetName, assetType);
    }
}
```

已实现：UnityEngine.Game.ResourcesLoader




##### 资源类

```c#
public class Assets
{
    public class Prefab
    {
        public readonly static IAssetLoader Character = new AssetBundlesLoader("Assets/Src/Prefabs/Character/");
        public readonly static IAssetLoader Effect = new AssetBundlesLoader("Assets/Src/Prefabs/Effect/");
    }

    public readonly static IAssetLoader Data = new AssetBundlesLoader("Assets/Src/Data/");

    public readonly static IAssetLoader Config = new AssetBundlesLoader("Assets/Src/Config/");
}
```







### 实例化

```c#
GameObject Instantiate(string assetBundleName, string assetName)
Task<GameObject> InstantiateAsync(string assetBundleName, string assetName)
```

实例化资源

**样例**

```c#
GameObject go = AssetBundles.Instantiate("<BundleName>", "<AssetName>");
```
异步实例化

```c#
var goTask = AssetBundles.InstantiateAsync("<BundleName>", "<AssetName>");
yield return goTask; //goTask.Result
```



### 卸载

```c#
void UnloadUnused(IEnumerable<string> assetBundleNames, bool allObjects = false)
```

- **assetBundleNames**


​	释放指定的 AssetBundle

- **allObjects**

​	将传递给 AssetBundle.Unload(allObjects)
释放未使用的AssetBundle，未使用的判断依据是检查是否存在引用 LoadAsset 时传递的 Owner 参数

**样例**

```c#
//加载AssetBundle
AssetBundles.LoadAssetBundle("<BundleName>");
//卸载AssetBundle
AssetBundles.UnloadUnused("<BundleName>");
```





## 资源规则

- Shader资源
  - 避免对内置 Shader 引用，资源路径为 `Resources/xxxx`，在官网下载 `builtin_shaders-xxxxxx.zip`，解压到工程内，使用工具替换已有的材质球shader
  - Shader单独打包会丢失变体 feature
    - feature 关键字存储在材质球，Shader与Material在同一个资源包
    - 使用 Shader Variant Collection 收集变体，菜单 Edit > Project Settings > Graphics Currently tracked xxx variants 点击 `Save to asset...` , 该文件与shader在同一个资源包



## 资源加载规则

- 加载资源包内Shader，手动加载
- 资源包资源引用Resources资源，生成时加入自动依赖资源包



## 资源名称类

由程序自动生成，便捷的获取资源路径，不用将资源路径硬编码在代码中

1. 勾选 `AssetBundleNames Class` 开启生成

2. `File Path` 设置生成位置

  默认值: `Assets/Plugins/gen/AssetBundleNames.dll`

3. `Asset Name Class` 设置默认类名

  默认值: `{$AssetPath:#DirectoryName}`

4. 点击菜单 `gen AssetBundleNames` 生成 `AssetBundleNames.dll`



类结构

```c#
class AssetBundleNames{
    string <BundleName>_AssetBundle = "<BundleName>";
    class <BundleName> {
		string[] <AssetName> = new string[] {<BundleName>_AssetBundle, "<AssetName>"};
		...
    }
    ...
}
```

程序使用

```c#
AssetBundles.LoadAsset<T>(AssetBundleNames.<BundleName>.<AssetName>);
```



## 预加载

AssetBundle 在初始化中提前加载，加载后的资源可以使用同步方法

2. 勾选 `预加载`  开启
2. 设置 `包含`与 `排除` 项，值为正则表达式格式
3. 预加载状态

   - AssetBundles.Status
  - Preloading
   
    预加载中
   
  - Preloaded
   
    预加载完成时状态
	
	  - AssetBundles.PreloadedTotal
	
	    预加载资源总数
	
	  - AssetBundles.PreloadedProgress
	
	    当前已加载资源数
	



## 变体

设置资源多个版本

比如性能版本：high, middle, low



1. `项/变体` 点击右边 `+` 按钮 添加变体配置

2. 设置 `变体` 名称，正则表达式格式

   ​	文件夹名称

   ```c#
   {$AssetPath:#DirectoryName}
   ```

   ​	正则表达式，result 组返回值

   ```c#
   {$AssetPath:#FileNameWithoutExtension:/.*_(?<result>.+$)/}
   ```

3. 设置 `过滤`，路径模式过滤

4. 程序配置

   ```c#
   //设置变体
   AssetBundles.Variants.Add("<Variant>");
   ```



## 解压

未实现






## 加密

保护资源，因为是对称加密，密匙保存在包体中可被破解

- 编辑器设置

	1. 勾选 `加密` 开启加密

	2. 设置 `加密密匙`，`加密 IV`，点击 `Gennerate` 按钮生成随机加密Key

	3. 设置加密过滤，如果 `包含` 和 `排除` 为空则默认所有都进行加密，值为正则表达式格式



## 签名

​	防止资源被串改，比如数据

- 编辑器设置

	1. 勾选 `签名` 开启签名
2. 设置 `签名Key路径` 点击 `Create` 按钮生成
	3. 设置签名公匙
4. 设置签名过滤 `包含` 和 `排除`，值为正则表达式格式


## 版本

版本文件

位置：清单位置

#### 版本文件

```
<Platform>.json
```

```json
{
    "platform": "<平台名称>",
    "bundleCode": <资源版本号>,
    "appVersion": "<应用版本号>",
    "timestamp": "<UTC时间戳>",
    "hash": "<清单哈希值(MD5)>",
    "commitId": "<Git提交ID>",
    "userData": "<用户数据>",
    "channel": "<渠道>",
    "groups": ["<local>","<remote>"]
}
```

- platform

  平台名称: Android, iOS, Windows

- bundleCode

  版本号, 从1开始

- appVersion

  应用版本号 `Application.version`，编辑器设置 `应用版本格式`

- timestamp

  生成时间戳，UTC毫秒值

- hash

  清单哈希值, 默认MD5值

- commitId

  Git提交ID

- userData

  用户数据

- channel

  渠道

- groups

  分组，local：本地资源组，remote：远程待下载资源组





## 下载更新

[下载更新](Doc/下载更新.md)





##  预览面板 [AssetBundle Info]

查看[配置文件](#配置文件)是否正确, 选中某个资源，在 **[Inspector]** 下方预览窗口标题选中 **[AssetBundle Info]**

- **Asset Path**

  当前资源的真实位置

- **Asset Name**

  生成 AssetBundle 时的资源名称

- **Asset Type**

  资源类型

- **Preloaded**

  是否为预加载资源

