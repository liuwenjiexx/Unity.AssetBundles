# String Format

扩展 `string.Format`



### 格式

- #### 调用方法

  **Format**
  
  ```
@[@][Type.]Member([arg0][,arg1]...)[,format]...
  ```

  调用方法

  - @

    参数实例 `GetType()` 类型

  - @@

    参数类型

  - Member

    参数成员(方法，属性，字段)

  - arg 

    参数

  - $

    引用当前实例

  - format

    返回值格式化
  
    
  
  引用当前实例
  
  ```c#
  //"ABC".ToLower()
  "{0:@ToLower()}".FormatString("ABC")
  => "abc"
  
  //"ABC".ToLower()
  "{0:@ToLower}".FormatString("ABC")
  => "abc"    
  
  //"ABC".Replace("BC","AA")
  "{0:@Replace(\"BC\",\"AA\")}".FormatString("ABC")
  => "AAA"
  
  //String.Join(" ","Hello","World")
  "{0:@String.Join(\" \",$,\"World\")}".FormatString("Hello")
  => "Hello World"
  
  //string.Empty    
  "{0:@Empty}".FormatString(typeof(string)))
  => ""
  
  //"ABC".Substring(1,2).ToLower()
  "{0:@Substring(1,2)@ToLower}".FormatString("ABC")
  => "bc"
      
  //string.Format("{0:yyyy",DateTime.Now)
  "{0:@Now,yyyy}".FormatString(typeof(DateTime))
  => 2019
  ```




- #### 正则表达式

    **Format**
    
    ```javascript
/(?<result>regex expression)/
    ```

    使用正则表达式提取字符串，提取内容result匹配组
    
    ```c#
    "{0:/(?<result>h.*d)/}".FormatString("say hello world .")
    => "hello world"
    ```





- #### 预设名称格式

  实现 `INameFormatter` 接口

  

  - ##### 路径格式

    ```
    Name[,Offset...][,SeparatorChar]
    ```

    **Name**

    - FilePath

      不做处理

    - FileName

      文件名
      
    - FileNameWithoutExtension
  
      不带扩展名的文件名
  
    - FileExtension
  
      文件扩展名
  
    - DirectoryName
  
      文件父目录名
  
    - DirectoryPath
  
      文件夹路径
  
    - FullPath
  
      完整路径名称
  
    - FullDirectoryPath
  
      完整文件夹路径
  
    **Offset:** 
  
    ​	目录偏移，数字，正数右边开始，负数左边开始
  
    **SeparatorChar:** 
  
    ​	`/`, `\\` 强制目录分隔符
  
     
  
    ```c#
  string path= "Dir/Sub/file.txt"
    
    "{0:#FilePath}".FormatString(path)
    => Dir/Sub/file.txt"
    
    "{0:#FileName}".FormatString(path)
    => "file.txt"
    
    "{0:#FileNameWithoutExtension}".FormatString(path)
    => "file"
    
    "{0:#FileExtension}".FormatString(path)
    => ".txt"
    
    "{0:#DirectoryName}".FormatString(path)
    => "Sub"
    
    "{0:#DirectoryPath}".FormatString(path)
    => "Dir/Sub"
    
    "{0:#DirectoryPath,1}".FormatString(path)
    => "Dir"
    
    "{0:#DirectoryPath,-1}".FormatString(path)
    => "Sub"
    
    "{0:#FilePath,\\}".FormatString(path)
    => "Dir\\Sub\\file.txt"
    ```
  
    
