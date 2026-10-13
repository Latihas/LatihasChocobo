> 有延迟裤链: https://raw.githubusercontent.com/Latihas/dalamud-plugins/main/repo.json
>
> 或无延迟裤链: https://github.com/Latihas/dalamud-plugins/releases/latest/download/repo.json
>
> 或CF加速裤链: https://dalamud-repo.latihas.workers.dev
>
> 请选择一个就行了，不同裤链之间的更新检查不通用

![](https://socialify.git.ci/Latihas/LatihasChocobo/image?description=1&forks=1&issues=1&language=1&name=1&owner=1&pattern=Transparent&pulls=1&stargazers=1&theme=Auto)

自动化陆行鸟竞赛。

支持循环匹配练级，一般1-40级需要2h。

稍微聪明一点的AI, 会吃道具用道具，会动态调整速度。

支持配种凭证筛选，看起来更方便

如果我AFK了或者忘记更新了，你也可以直接修改Constant.cs中的OPCODE_DUTY (用任意抓包软件即可, 本人喜欢https:
//github.com/extrant/FFXIVNetworkPacketAnalysisTool),注意顺序,实际使用在Plugin.cs的RequestRace函数