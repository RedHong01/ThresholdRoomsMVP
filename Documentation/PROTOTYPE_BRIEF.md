# FrontRooms — Functional Prototype / 原型说明

本文件说明设计依据、实现范围和课堂讨论准备。构建与运行结果见[验证记录](VALIDATION.md)；制作观察不代替真人试玩反馈。

## 这周作业要交什么

作业截图要求一个有交互、包含核心素材的可执行 build，以及 **Diary Entry #2**：记录 pitch 得到的反馈，以及这些反馈如何改变自己的想法。Functional Prototype 用来判断规模和检验 Razor & Outline 中的风险；这周不要求已经好玩，也不等同于 First Playable。下次课堂 critique 在 8:30 p.m. 分享，需能回答原型的问题、制作中学到什么、什么出乎意料。

**Diary #2 的真实 pitch feedback 尚未提供。** 下方只有可核对的制作观察和待填写项，不能作为已完成的个人反馈日记提交。

## 我们在验证什么

设计指导文件：[FrontRooms deck](https://www.figma.com/deck/NmYGRYKlhfX6H4rbJ7QcSN)。它的核心问题是：**为了获取信息，值得花掉多少逃生时间？** 玩家进入房间，观察并读取线索，决定拿取资源或立即逃跑，再选择出口。读取会占用约 1.5 秒；这段时间内追猎者仍在行动。信息要对下一步选择有用，停下来的风险才有意义。

这轮把问题压到一条 **1×5 的短路线**：Lobby → Level 0 → Level 4 → Level ! → Exit。中间三格分别验证移动中的空间变化、办公室钥匙与出口选择、红色追逐区。deck 的 8–12 分钟是目标体验规模；当前短切片的时长需实际测量，不能当成已完成的 8–12 分钟版本。

| 房间 / 系统 | 当前原型要表达的行为 | 本轮简化与边界 |
| --- | --- | --- |
| Lobby | 提示安静行走有助于甩掉调查；奔跑与拾钥匙发出噪声，破玻璃能传遍关卡。 | 起始教学区，不额外扩展一类关卡。 |
| Level 0 | 嗡鸣在变化前一秒降低；把通道保持在屏幕内可阻止它变化。 | 用相机是否看见一对通道，决定能否交换其封闭状态；不是完整的可变建筑或玩家视线模拟。 |
| Level 4 / Office | 笔记指出钥匙在东北角；关闭的门让追猎者破门 2.5 秒，破窗后通路持续敞开。 | 同一面共享墙还提供高房间拥有的玻璃窗，便于在短切片内直接比较钥匙耗时与破窗噪声。 |
| Level ! / Run | 红色空间发出立即逃跑的信号，最终通过窗进入出口区。 | 灰盒的色彩、声音与追逐压力代理完整环境叙事。 |
| 读取与地图 | 在白色提示附近按住 Q，完成读取后把规则固定到地图；地图保留已读信息。 | 三张笔记作为信息收集的可测量代理，不是自动计算最佳路线。 |
| 结局 | 界面仅显示 Escaped / Caught、用时和已读笔记数量。记录仍以三张笔记加办公室钥匙区分 informed escape 与 fast escape。 | 这项记录是 rich escape 的原型代理；尚不是完整奖励、叙事或多结局系统。 |

出口仍遵循 deck 的高度语言：低房间拥有 hall，标准房间拥有 door，高房间拥有 window。办公室的门和窗口连接同一个后续房间，窗口属于另一侧的高房间。这样保留出口的来源规则，同时让这一轮确实有“找钥匙还是冒险破窗”的分支，而不是把全部出口排成唯一必经顺序。

读取、停留、查看地图和逃跑都应参与同一个时间压力。当前相机代理有明确限制：打开总览地图会把通道纳入视野，从而阻止其变化。这是本轮可观察的灰盒规则，后续需要验证它是否让玩家轻易取消空间变化的风险。

## 本轮收到的设计反馈 / Feedback from this session

用户在本轮明确要求简化 UI，并增加接近 *Dark Deception*、*Escape the Backrooms* 体验的第一人称 3D 版本。2D 版本的常驻信息已收敛到房间名、追猎者距离和一条当前相关提示；移除了悬浮出口标签以及持续显示的操作与统计信息。完整操作放在 Esc 暂停菜单，标题等待 Space 开始，标题界面禁用 C 切换；结算只保留结果、用时和笔记数量。

[FrontRooms3DMVP](../../FrontRooms3DMVP) 是接下来用于比较的第一人称版本，构建和实际运行仍待验证。本轮比较的意图是观察：改成第一人称后，玩家如何辨认房间规则、感受追逐距离，以及决定是否停下读取。尚未取得 2D / 3D 的玩家对比结果。

以上是**本次制作过程中用户直接给出的设计反馈**，不是课堂 pitch feedback。Diary #2 所需的真实课堂反馈仍未提供。

## 与 Curtain 的关系

deck 提出建立在 Curtain 之上。这里延续的是已审阅的设计结构：发现玩家、追踪、追逐以及门阻挡时破门。当前 FrontRooms 是独立编写的适配版本，没有把 Curtain 的代码或美术、音频素材直接搬入工程。Curtain 的视觉侦测／搜索／破门逻辑，与本轮围绕噪声调查并结合视线追逐的状态机，不能当成同一套已经验证的系统。

本轮核心素材采用可替换的灰盒房间、出口、玩家与追猎者标识、可读提示、地图界面和程序生成音效。它们用于表达交互，不代表 deck 中的最终环境素材、完整语音线索或视觉完成度。

## 操作 / Controls

| 输入 | 行为 / Action |
| --- | --- |
| Space / Enter | 在标题界面手动开始 / Start from the title. |
| WASD 或方向键 | 移动 / Move. |
| Shift | 奔跑，产生更明显的噪声 / Run; louder footsteps. |
| 按住 E | 在窗边持续破玻璃 / Hold near a window to break it. |
| 按住 Q | 靠近白色提示时停下并读取约 1.5 秒 / Hold beside a white note to read and pin it. |
| C | 开始后切换键盘与光标移动；标题界面禁用 / Toggle controls after starting; disabled on the title. |
| 光标模式：按住鼠标左键 | 向光标移动；Shift 仍可奔跑 / Hold LMB to move toward the cursor. |
| 光标模式：按住鼠标右键 | 在窗边破玻璃 / Hold RMB near glass to break it. |
| Tab | 显示或收起地图与已读笔记 / Toggle map and pinned notes. |
| Esc | 暂停并查看完整操作／继续 / Open pause and controls, or resume. |
| R | 在暂停或结算界面重试 / Retry from pause or results. |
| F | 切换全屏 / Toggle fullscreen. |

钥匙靠近后自动拾取；拥有对应钥匙后，靠近门即可开门。读取时追猎者不会因此暂停。地图也不是暂停界面。

## Critique preparation — English

**What question is this prototype trying to answer?**

How much information is worth the seconds it costs to collect? The test asks whether stopping to read a room's rule, while a hunter approaches, changes a player's next action. The office makes the tradeoff concrete: spend time finding a key and use a door that can delay pursuit, or break a window and make a loud noise.

**What did building it reveal? — implementation observations, not playtest findings**

The design needed an explicit place to read, an intentional hold, and a persistent record of what was learned. Otherwise, “reading the room” was difficult to distinguish from simply standing still. The three readable notes and map pins make that action observable. The short route also needed two exits on the office boundary so that door versus window becomes a choice that can be compared.

The build records room dwell time, selected exits, reading events, noises, and hunter distance in prototype world units. Those records can show what happened. They do not, by themselves, establish why a player chose an exit or whether the decision felt meaningful.

**What surprised you? — prompt to complete after running and testing**

No personal surprise or player reaction has been established yet. One implementation issue worth investigating is that map visibility affects the shifting-room rule. Another is whether the time saved by the window is enough to offset its noise. Record an observed result and its timestamp or run before turning either into a critique claim.

**Next observation to collect**

Compare a quick escape attempt with an attempt that reads all three notes. At the office, record the chosen exit, time spent, distance to the hunter and the player's own explanation. Then compare cursor movement with WASD. Use the results to tune reading time, noise reach and pursuit speed; do not expand the room count merely to reach a target duration.

## Diary Entry #2 — working draft, incomplete

**[PENDING — actual pitch feedback from the designer/class; not supplied]**

- Who gave the feedback, and what did they say? **[TO BE FILLED WITH ACTUAL FEEDBACK]**
- How did that feedback change my thinking? **[TO BE FILLED BY THE DESIGNER]**
- Which change did I make because of that feedback? **[TO BE FILLED; DO NOT ATTRIBUTE THE IMPLEMENTATION CHANGES BELOW TO FEEDBACK WITHOUT EVIDENCE]**

**Draft building observations / 制作观察草稿**

The current slice makes the information/time tradeoff explicit through a nearby note, a timed read and a map pin. It limits the route to three representative room types between a lobby and an exit. The office offers both a keyed door and a noisy window so the comparison can happen inside one short run. The shifting-room behavior uses camera visibility as a practical greybox approximation. These are implementation decisions; whether they support the intended tension remains a playtest question.

这段只能作为制作过程素材。真实 pitch feedback、本人如何理解反馈，以及实际测试后有什么惊讶或收获，仍需本人补充。

## Evidence boundary / 证据边界

| Evidence | Status in this brief |
| --- | --- |
| Assignment screenshot | 已读取；交付物为交互 build 与 Diary #2。 |
| Figma design guidance | 已审阅；下面列出对应标题及节点。 |
| Current implementation | 可从项目源码核对设计落点；源码不等于运行成功。 |
| Unity compilation and exported builds | macOS 构建已完成，包含 arm64 与 x86_64；最终交付详情见验证记录。 |
| Automated route and logic checks | 待最终验证记录确认；本文不宣称通过。 |
| Interactive runtime and audiovisual checks | 待最终验证记录确认。 |
| Feedback from this session | 用户已要求简化 UI 并增加第一人称 3D 对比；上文记录其对应改动与待验证范围。 |
| Real-player comparison of 2D / 3D | 尚未取得；3D 构建与运行待验证，自动驾驶测试不能替代玩家比较。 |
| Pitch feedback / complete Diary #2 | 缺少本人或课堂真实反馈，未完成。 |

Source references use slide titles and node IDs because deck slide names/numbering are inconsistent:

- Design question — `2:63`.
- One loop: enter / read / take or run / leave — `2:69`.
- Three ways out / room height — `64:2`.
- Room rules: Level 0, Level 4, Level ! — `2:75`.
- Scope: three rooms, three exit types, one hunter, five verbs, 8–12 minutes; fast and rich escape — `2:87`.
- Diary 1 / prototype direction: 1×5 greybox, approximately 1.5-second tell reading, map pins, cursor versus WASD, stopwatch — `67:1233`.
# First-person 3D comparison

The original 2D FrontRooms slice remains the functional prototype and primary assignment build. The separate 3D folder is a comparison experiment requested during this session: the same five-room logic is re-authored as real first-person geometry so perspective, room readability, and pursuit pressure can be evaluated independently. The 3D build is not evidence of a finished 8–12 minute game or human playtest.
