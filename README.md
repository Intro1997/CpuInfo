# CPU INFO

此项目借助 [https://github.com/openhardwaremonitor/openhardwaremonitor](https://github.com/openhardwaremonitor/openhardwaremonitor) dll 获取 CPU 信息，目前支持获取 CPU 的 TEMP CLOCK

# 使用说明：

下载后需要使用管理员身份打开
CLI 参数如下

```
-f, --flush        (Default: false) Flush console each output.
-i, --info-list    Required. All cpu info that you want to output.
-t, --time         (Default: 1000) Set millisecond internal time of update, cannot less than 1000.
-e, --exit-sign    (Default: ) Set exit input sign, exp: -e exit means when you input 'exit' in console, the
                    application will exit.
--help             Display this help screen.
--version          Display version information.
```

其中 time 用 int 存储。

# 开发说明

1. 命令行参数提取工具使用的是 [https://github.com/commandlineparser/commandline](https://github.com/commandlineparser/commandline)，需要配置 nuget 使用，使用方法参照 [https://learn.microsoft.com/zh-cn/nuget/quickstart/install-and-use-a-package-in-visual-studio](https://learn.microsoft.com/zh-cn/nuget/quickstart/install-and-use-a-package-in-visual-studio)
