# Unity.AssetBundles



## 预览


![Settings](Assets/Plugins/Unity.AssetBundles/Doc/settings.png)



![Group](Assets/Plugins/Unity.AssetBundles/Doc/group.PNG)



## 快速使用

1. 添加依赖 `< Project Dir>/Packages/manifest.json`， Unity 2019.4 支持 git URL

   ```
   {
     "dependencies": {
       "unity.extensions": "https://github.com/liuwenjiexx/Unity.Extensions.git?path=/Assets",
       "unity.guiextensions": "https://github.com/liuwenjiexx/Unity.GuiExtensions.git?path=/Assets",
       "unity.localization": "https://github.com/liuwenjiexx/Unity.Localization.git?path=/Assets",
     	...
     	}
   }
   ```



2. 包位置 URL

   ```
   "unity.assetbundles": "https://github.com/liuwenjiexx/Unity.AssetBundles.git?path=/Assets/Plugins/Unity.AssetBundles"
   ```

3. `Assets/Example/Src` 测试资源的目录

4. `AssetBundles/Windows` 测试输出目录

5. `Assets/Example/AssetBundlesExample.cs` 使用样例

   



[详细介绍](Assets/Plugins/Unity.AssetBundles/README.md)

