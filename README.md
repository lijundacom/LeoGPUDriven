[toc]
#1 前言
最近阅读了[GDC|Terrain Rendering in 'Far Cry 5'](https://www.gdcvault.com/play/1025480/Terrain-Rendering-in-Far-Cry) 和[TGDC | 用技术诠释国风浪漫的归去来 ——《天涯明月刀》手游开发历程](https://mp.weixin.qq.com/s/p3lF-UHtnlSbziQgvEiqqA) 的文章后，想要自己尝试实现一下GPUDriven技术。

先放一个最终效果<br>
![效果动图.gif](https://km.woa.com/asset/eeeef48c51d548919910b0b66d368afa?height=548&width=1867)<br>

该项目只是自己的学习项目，并未经过实际线上项目的检验。该文章只包含地形Mesh的渲染，不包含光照、阴影、材质贴图的渲染和资源流式加载等内容。

该项目使用Unity的URP管线，可以在PC和Android上运行。PC支持DX11，Android支持OpenGL ES3和Vulkan。iOS没有做适配。

文章结尾会放出源码链接。
#2 整体结构
使用HeighfildMap和四叉树LOD方法的地形渲染，一般经过以下几个步骤：
- 1）四叉树方法计算地形LOD  
- 2）视锥体剔除
- 3）提交渲染

其中第1步四叉树方法计算地形LOD和第2步剔除是在CPU中完成的，渲染是在GPU中完成的。
然而在大世界地形渲染中，第1步和第2步会随着地形规模的增大，逐渐提高CPU耗时，直到不可接受。

GPUDriven技术，就是将第1步和第2步也挪到GPU中执行，利用GPU的并行计算特性，提高计算效率。

传统地形渲染流程和GPUDriven地形渲染流程的对比图如下：<br>

![结构对比.png](https://km.woa.com/asset/d4070a712b2a4ce4a00a9e9f5aef7498?height=933&width=3307)<br>

本项目的GPUDriven地形渲染的功能模块和运行流程，如下图：<br>

![整体结构.png#500px#auto#center](https://km.woa.com/asset/ce3423e3b6ef4473a10b7e0ee4fbbfa7?height=1897&width=2910)<br>

以上步骤均在GPU中执行。首先计算地形LOD，生成Node列表，同时生成SectorLodMap用于之后的相邻的不同LOD的Mesh接缝处理。接着将Node列表经过视锥体剔除，将剔除后的Node列表，变换成Patch列表。再将Patch列表经过Hiz遮挡剔除。最后将剩余Patch通过Instance方式提交渲染。

本文将分别讲解其中每个模块的原理和实现方式。

#3 参数与定义

这里定义了一些概念。
- **World**:我要实现的Terrain是10240m x 10240m，我称之为World。

- **LOD**:整个World包含6级LOD，分别为LOD0,LOD1,LOD2,LOD3,LOD4,LOD5。

- **Node**：LOD5时，整个World被分成5x5个Node，那么：

- LOD5: Node Num = 5x5，    Node Size = 2048m x 2048m
- LOD4: Node Num = 10x10，  Node Size = 1024m x 1024m
- LOD3: Node Num = 20x20， Node Size = 512m x 512m
- LOD2: Node Num = 40x40， Node Size = 256m x 256m
- LOD1: Node Num = 80x80， Node Size = 128m x 128m
- LOD0: NodeNum = 160x160， Node Size = 64m x 64m

- **Patch**:每个Node，分成8x8个Patch，那么：
- LOD0: Patch Size = 8m x 8m
- LOD1: Patch Size = 16m x 16m
- LOD2: Patch Size = 32m x 32m
- LOD3: Patch Size = 64m x 64m
- LOD4: Patch Size = 128m x 128m
- LOD5: Patch Size = 256m x 256m

- **Sector**:我们将每8m x 8m大小（即LOD0 Patch大小）的区域称之为一个Sector。则整个World被分为1280 x 1280个Sector.

- **Mesh**:构建一个正方形Mesh，包含顶点数17x17,尺寸8m x 8m, 每个格子0.5m x 0.5m。这个8m x 8m的Mesh对应LOD0的Patch。之后通过调整Mesh的缩放来对应其他LOD级别的Patch。Mesh结构如下：

![Mesh.png#300px#auto#center](https://km.woa.com/asset/3e16afa6a8aa44eba537e57550a3d5cd?height=2150&width=2291)<br>

#4 地形四叉树LOD
##4.1 四叉树LOD
初始时，currentLod=LOD5，世界被分割为5x5个Node。如下图：<br>

![tile.png#300px#auto#center](https://km.woa.com/asset/9d5e5c8a0b3a4431accf2064ab470e36?height=2275&width=2287)<br>

接着进行currentLod=LOD4时的四叉树分割，我定义了以下公式，用于判断某个Node是否需要被四叉树分割:

$ isNeedLod = \frac{LodJudgeFector * NodeSize}{distance * FOV} + detailBias$

其中$isNeedLod$的数值作为判断是否继续细分的依据，如果$isNeedLod > 0$，则当前LOD需要被四叉树分割；如果$isNeedLod  <= 0$，则当前LOD不需要被四叉树分割。

$isNeedLod$的结果被$LodJudgeFector$、$NodeSize$、$distance$、$FOV$、$detailBias$几个因子影响。

$LodJudgeFector$：业务侧的一个调整因子，调大这个因子，则地形倾向于被分割的更细，网格密度更高。

$NodeSize$:某一LOD级别，Node的尺寸

$distance$:Node包围盒与摄像机的距离。想要计算距离，还需要知道Node的地形高度。之后会用离线预烘焙MinMaxHeightMap来解决。

$FOV$:摄像机的视野大小Fild of View.

$detailBias$:细节调整因子。这个在《天刀》的分享里有提到。目的是，有些地块比较平整，需要较少密度的Mesh网格就可以渲染。有些地块比较崎岖，需要较高密度的Mesh网格来渲染。如果只以摄像机距离作为判断是否LOD细分的判断依据，有些比较崎岖的地表就会渲染精度不足。这个$detailBias$是离线预烘焙的，记录哪些地块需要提高LOD级别。本项目没有实现这部分功能。

如下图：平整地表（蓝框）和崎岖地表（红框）用了相同的网格密度，其实红框区域可以用更高一级的LOD去渲染。<br>

![detailbias.png#300px#auto#center](https://km.woa.com/asset/bb7edb4a4bc846468f1ff5e541c5bc5c?height=601&width=997)<br>

通过计算LOD5级别下，每个Node的$isNeedLod$，就可以得到LOD4的Node列表，如下图：<br>

![LOD4.png#300px#auto#center](https://km.woa.com/asset/083c7386162e4d52b3ff98a58cd41567?height=2275&width=2287)<br>
取出LOD4的所有Node，再次计算$isNeedLod$，可得到LOD3的Node列表，如下图：<br>

![LOD.png#300px#auto#center](https://km.woa.com/asset/93f0b6f5cda64fefb1e4bfa0ec664a44?height=2275&width=2287)<br>
以此类推，最终得到LOD0的列表。在GPU中实现思路就是以上。
## 4.2 ComputeShader中实现四叉树LOD
ComputeShader中想要实现4.1中的LOD算法，
需要定义以下3个Buffer结构：<br>
``` 
struct NodePatchStruct
{
    uint2 NodeXY;
    uint2 PatchXY;
    uint LOD;
    int4 LodTrans;
    float3 boundMin;
    float3 boundMax;
};

uniform uint CURRENT_LOD;
AppendStructuredBuffer<NodePatchStruct> finalList;
AppendStructuredBuffer<NodePatchStruct> appendList;
ConsumeStructuredBuffer<NodePatchStruct> consumeList;
```

其中，NodePatchStruc时Node和Patch的共用结构，目的是复用StructuredBuffer，减少StructuredBuffer的数量，有些手机上StructuredBuffer的数量有限制。

CURRENT_LOD表示算法执行的当前LOD。


具体算法如下:
1. 将CURRENT_LOD设为5
2. 执行Dispatch
3. 从consumeList中获取节点，对节点进行评价，决定是否分割。
4. 如果决定分割，那么将分割后的4个节点加入appendList
5. 否则将当前节点加入finalList
6. Dispatch结束
7. 将当前CURRENT_LOD减1，互换ConsumeNodeList和AppendNodeList，回到2执行下一个Pass
kernal代码如下：

``` 
//计算Node的包围盒，从MinMaxHeightMap拿到高度数据
void CalNodeBound(GlobalValue gvalue, inout NodePatchStruct nodeStruct)
{
    float2 height = MinMaxHeightMap.mips[nodeStruct.LOD + 3][nodeStruct.NodeXY].xy;
    float2 minMaxHeight = (height - 0.5) * 2 * gvalue.worldHeightScale;
    float nodeSize = GetNodeSizeInLod(gvalue, nodeStruct.LOD);
    nodeStruct.boundMax = float3(nodeSize * 0.5, minMaxHeight.y, nodeSize*0.5);
    nodeStruct.boundMin = float3(nodeSize * -0.5, minMaxHeight.x, nodeSize * -0.5);
}

//是否需要四叉树的判断函数
uint IsNeedQuad(GlobalValue gvalue, uint2 nodeXY, float maxHeight,uint LOD)
{
    if (LOD == 0)
    {
        return 0;
    }
    float3 cameraWorldPos = gvalue.cameraWorldPos;
    float fov = gvalue.fov;
    float nodeSize = GetNodeSizeInLod(gvalue, LOD);
    float2 nodePos = GetNodeCenerPos(gvalue, nodeXY, LOD);
    float dis = distance(cameraWorldPos, float3(nodePos.x, maxHeight, nodePos.y));
    float result = gvalue.LodJudgeFector * nodeSize / (dis * fov);
    return step(1, result);
}

//每Dispatch一次，就会生成下一级四叉树LOD
[numthreads(1, 1, 1)]
void NodeQuadLod(uint3 id : SV_DispatchThreadID)
{
    GlobalValue gvalue = GetGlobalValue(globalValueList);
    NodePatchStruct nodeStruct = consumeList.Consume();
    uint2 nodeXY = nodeStruct.NodeXY;
    nodeStruct.LOD = CURRENT_LOD;
    CalNodeBound(gvalue, nodeStruct);
    int nodeIndex = GetNodeIndex(gvalue, nodeXY, CURRENT_LOD);
    uint needQuad = IsNeedQuad(gvalue, nodeXY, nodeStruct.boundMax.y, CURRENT_LOD);
    if (needQuad == 1)
    {
        NodePatchStruct nodeStruct0 = CreateEmptyNodePatchStruct();
        NodePatchStruct nodeStruct1 = CreateEmptyNodePatchStruct();
        NodePatchStruct nodeStruct2 = CreateEmptyNodePatchStruct();
        NodePatchStruct nodeStruct3 = CreateEmptyNodePatchStruct();
        nodeStruct0.NodeXY = nodeXY * 2;
        nodeStruct1.NodeXY = nodeXY * 2 + uint2(0, 1);
        nodeStruct2.NodeXY = nodeXY * 2 + uint2(1, 0);
        nodeStruct3.NodeXY = nodeXY * 2 + uint2(1, 1);
        
        appendList.Append(nodeStruct0);
        appendList.Append(nodeStruct1);
        appendList.Append(nodeStruct2);
        appendList.Append(nodeStruct3);
        NodeBrunchList[nodeIndex] = 1;
    }
    else
    {
        finalList.Append(nodeStruct);
        NodeBrunchList[nodeIndex] = 2;
    }
}
```
其中**IsNeedQuad**就是计算4.1中$IsNeedLod$的方法，计算$IsNeedLod$需要知道Node中心点的高度，**CalNodeBound**方法用于获取Node的包围盒，包围盒高度来自MinMaxHeightMap。

C#层，每次Dispatch  Kernal:NodeQuadLod一次，就进行了一层LOD的分割。经过5次Dispach，就依次计算出了LOD5,LOD4,LOD3,LOD2,LOD1的四叉树分割情况。并将有效Node保存到了finalList中。

而每次Dispatch，是将上一次Dispatch的结果（appendList）作为这一次Dispatch的输入（consumeList）。所以这里需要一个PingPang操作，来交换appendList和consumeList。

``` 
for (int i = TerrainDataManager.MIN_LOD; i >= 0; i--)
{
            command.SetComputeIntParam(CS_GPUDrivenTerrain, ComputeShaderDefine.CURRENT_LOD_P, i);

            command.SetComputeBufferParam(CS_GPUDrivenTerrain, KN_NodeQuadLod, ComputeShaderDefine.consumeList_P, nodeBufferPing);
            command.SetComputeBufferParam(CS_GPUDrivenTerrain, KN_NodeQuadLod, ComputeShaderDefine.appendList_P, nodeBufferPang);
            command.DispatchCompute(CS_GPUDrivenTerrain, KN_NodeQuadLod, mDispatchArgsBuffer, 0);

            command.CopyCounterValue(nodeBufferPang, mDispatchArgsBuffer, 0);

            ComputeBuffer temp = nodeBufferPing;
            nodeBufferPing = nodeBufferPang;
            nodeBufferPang = temp;
}
```


##4.3 离线预烘焙MinMaxHeightMap
4.2章节中**CalNodeBound**方法中采样了**MinMaxHeightMap**。**MinMaxHeightMap**格式为RGFloat。像素值记录的是，离线预计算的每个LOD级别的，每个Patch的包围盒的高度最低值和高度最高值。R通道存储一个Patch的包围盒的高度最低值，G通道存储一个Patch的包围盒的高度最高值。Patch的LOD对应**MinMaxHeightMap**的Mip。

**MinMaxHeightMap**存储的是Patch的包围盒的高度值，也就是存储了Node的包围盒的高度值。因为同一个Lod的Patch和Node在**MinMaxHeightMap**中，mip相差3.

工程里有个Editor工具实现了这个功能。

##4.4 生成SectorLODMap
生成**SectorLODMap**的目的是记录Terrain中，每个Node的LOD级别，用于之后地形渲染时，处理不同LOD级别的Patch的Mesh的接缝。<br>

![TerrainLodMap.png#500px#auto#center](https://km.woa.com/asset/9e28748efe9c4bc4bef55e72df3cded8?height=896&width=1579)<br>

**SectorLODMap**的像素数量是160x160，也就是LOD0时，Node的数量。Sector（LOD0的Patch）的数量是1280x1280，但是每8x8个Patch组成一个Node，所以每8x8个Patch的LOD都是相等的，等于自己所在Node的LOD，所以**SectorLODMap**只需存160x160个数据。

之后一个Patch想要知道自己的LOD级别，只需计算出自己覆盖了哪些Sector，然后再通过Sector算出**SectorLODMap**对应哪个像素。这个像素值就是Patch的LOD。

4.2章节中**NodeBrunchList**记录了每个LOD级别的Node是否被进一步细分。只有值为2的Node，表示是不需要被细分的Node，才是最终提交给后续流程的有效Node，其他节点都是无效Node。

生成**SectorLODMap**的算法如下，本质上就是在一颗完全四查树中，按层数从上至下，找到第一个值为2的父Node，并记录这个父Node的LOD值。
``` 
[numthreads(8, 8, 1)]
void CreateSectorLodMap(uint3 id : SV_DispatchThreadID)
{
    GlobalValue gvalue = GetGlobalValue(globalValueList);
    uint2 sectorId = id.xy;
    for (int i = gvalue.MIN_LOD; i >= 0; i--)
    {
        //cal nodeXY in LOD0 locate in which node in LOD i
        int2 nodeXY = sectorId >> i;
        int nodeIndex = GetNodeIndex(gvalue, nodeXY, i);
        uint isbrunch = NodeBrunchList[nodeIndex];
        if (isbrunch == 2)
        {
            SectorLODMap[sectorId] = i * 1.0 / gvalue.MIN_LOD;
            return;
        }
    }
}
```

#5 视锥剔除
此时，finalList已经存储了四叉树分割后的有效Node列表。这些Node需要经过视锥体剔除。

视锥体剔除的基本原理，就是检测一个立方体包围盒是否与一个平截头体碰撞。

这个问题看起来好像挺简单的，似乎高中立体几何就能解决。其实不然。

面对这个问题，我们自然而然想到的方法就是，判断这个立方体包围盒有没有顶点位于平截头体内。如果包围盒存在位于平截头体内的顶点，说明包围盒与平截头体碰撞，保留不剔除。如果立方体包围盒8个顶点都不在平截头体内，说明立方体包围盒与平截头体不相交，那么这个物体就需要被剔除。但这个想法对吗？看下面下图。<br>

![fcull0.png#500px#auto#center](https://km.woa.com/asset/a89f9130b8474c628557a27223d97e0b?height=842&width=1666)<br>

按照上述算法，图中黑色、红色包围盒被剔除，蓝色、黄色包围盒不被剔除。你会发现，红色包围盒被错误剔除了，但是红色包围盒确实8个顶点都不在平截头体内。如果游戏中，这个红色包围盒表示一个大的建筑，或者墙，上面的算法就会错误剔除。

其实立方体包围盒与平截头体碰撞问题并不是一个简单的问题，没有简便的算法求出精确解。大家感兴趣的可以去搜索一下。

目前，为了提高算法速度，普遍采用保守剔除算法。就是用一种速度快的剔除算法，剔除大部分需要被剔除的包围盒，少部分包围盒虽然没有与视锥体碰撞，但是算法就不剔除他了，继续提交渲染，反正最后也不会出现在屏幕里。

一种常用的保守剔除算法，是判断立方体8个顶点是否在视锥体的6个面外，如果8个顶点同时在视锥体的某个面外，就剔除这个包围盒，否者不剔除。<br>

![fcull1.png#500px#auto#center](https://km.woa.com/asset/47b378a4fa314df1ba44aa7981cb1259?height=1953&width=2741)<br>

按照以上算法，发现绿色、黄色、蓝色、红色包围盒被保留，黑色包围盒被剔除。其中绿色包围盒没有与视锥体碰撞，但是没有被剔除，而是提交渲染了。这就是保守剔除，少部分该剔除的没有被剔除掉。但是这并不会引起渲染错误，绿色包围盒中的物体并不会出现在屏幕中。

判断一个点是否在一个平面外的算法是：<br>

![相交.png#200px#auto#center](https://km.woa.com/asset/96045773fafe404b8238f2c9e7177c6a?height=1335&width=1172)<br>

设P点的坐标是（i,j,k），平面法线N的方向是（a,b,c），平面到原点的距离w。定义法线方向的空间为平面内，法线反方向的空间为平面外。

则平面方程可以表示为:
ax + by + cz + w = 0;

计算点P位于平面哪一侧的方法是：将点P的坐标带入平面方程。

如果ai + bj + ck +w < 0, 则点P在平面内（法线方向空间）；

如果ai + bj + ck +w = 0，则点P在平面上；

如果ai + bj + ck +w > 0, 则点P在平面外（法线反方向空间）；

将包围盒的8个顶点和视锥体6个面的平面方程用上面的公式计算，既可以算出包围盒是否被裁剪。

但是要计算8x6=48次，计算量有些大。这个算法有个优化算法来提高计算速度。不需要计算8个顶点是否在6个面外。经过优化后，只需要计算2个顶点是否在6个面外。

优化算法是计算出包围盒8个点中距离平面最近的点$P_{near}$和距离平面最远的点(最远对角线的顶点)$P_{far}$。<br>

![相交1.png#500px#auto#center](https://km.woa.com/asset/5f80c2c56d6d4bdbadb7e70dd21aa4a4?height=1335&width=1910)<br>

如果$P_{near}$在平面外部，则立方体在平面外部。

如果$P_{near}$和$P_{far}$一个在平面内部，一个在平面外部，则包围盒与平面相交。

如果$P_{near}$和$P_{far}$都在平面外部，则包围盒在平面外部。

计算$P_{near}$和$P_{far}$的方式是：

首先计算$P_{max}$和$P_{min}$

$P_{max} = max(P_0,P_1,P_2,P_3,P_4,P_5,P_6,P_7)$

$P_{min} = min(P_0,P_1,P_2,P_3,P_4,P_5,P_6,P_7)$

然后

$P_{near} = P_{min}$, <br>
$P_{far} = P_{max}$,

最后

$if(N.x > 0)  P_{near}.x = P_{max}.x; $
$if(N.y > 0)  P_{near}.y = P_{max}.y; $
$if(N.z > 0)  P_{near}.z = P_{max}.z;$

$if(N.x > 0)  P_{far}.x = P_{min}.x;  $ 
$if(N.y > 0)  P_{far}.y = P_{min}.y;$
$if(N.z > 0)  P_{far}.z = P_{min}.z;$

ComputeShader的代码如下：

```
bool IsOutSidePlane(float4 plane, float3 position)
{
    return dot(plane.xyz, position) + plane.w < 0;
}

//true: avalible
//flase: culled
bool FrustumCullBound(float3 minPos, float3 maxPos, float4 planes[6])
{
    [unroll]
    for (int i = 0; i < 6;i++)
    {
        float3 p = minPos;
        float3 normal = planes[i].xyz;
        if (normal.x >= 0) 
            p.x = maxPos.x;
        if (normal.y >= 0)
            p.y = maxPos.y;
        if (normal.z >= 0)
            p.z = maxPos.z;
        if (IsOutSidePlane(planes[i], p))
        {
            return false;
        }
    }
    return true;
}

[numthreads(1, 1, 1)]
void FrustumCull(uint3 groupId : SV_GroupID, uint3 idInGroup : SV_GroupThreadID)
{
    GlobalValue gvalue = GetGlobalValue(globalValueList);
    NodePatchStruct nodeStruct = consumeList.Consume();
    float2 center = GetNodeCenerPos(gvalue, nodeStruct.NodeXY, nodeStruct.LOD);
    float3 center3 = float3(center.x, 0, center.y);
    float4 frustumPlane[6];
    GetFrustumPlane(globalValueList, frustumPlane);
    bool frusAvalible = FrustumCullBound(center3 + nodeStruct.boundMin, center3 + nodeStruct.boundMax, frustumPlane);
    if (frusAvalible)
    {
        appendList.Append(nodeStruct);
    }
}
```

摄像机视锥体的6个面的平面方程，可以通过GeometryUtility.CalculateFrustumPlanes得到。另外，Unity视锥体的6个面的法线方向是向内的。

#6 Hiz遮挡剔除
此时，我获得了经过视锥体剔除后的NodeList，每个Node有8x8各Patch，可根据NodeList生成PatchList，然后将PatchList喂给Hiz遮挡剔除模块。
## 6.1 Hiz遮挡剔除原理
Hiz剔除指Hierarchical-Z遮挡剔除。原理是根据场景的深度图，为深度图生成Mip结构。不过这个Mip图中的像素值不是取上一级Mip的2x2个像素的平均值，而是取这2x2个像素的深度最深值。深度图以及得到的一系列Mip图，称为Hiz Map。最小的Mip，只有1x1个像素。如下图:<br>

![Hiz Cull.png#500px#auto#center](https://km.woa.com/asset/bda138da777f4c6d8a35f35ea5c072e8?height=887&width=1581)<br>

得到Hiz Map图后，经过以下算法步骤，可进行遮挡剔除：

- 1）首先将物体的包围盒的8个顶点投影到NDC空间，得到屏幕空间的8个点。
- 2）针对这8个点建立NDC空间的AABB Box，得到一个屏幕空间矩形.
- 3）根据这个矩形的最大边长，计算出一个合适的Mip。在这个Mip下，矩形的4个顶点恰好满足位于相邻的2x2个像素。
- 4）依次对4个顶点进行深度测试，如果均未通过，那么就意味着这个物体被完全遮挡。<br>

![hiz.png#300px#auto#center](https://km.woa.com/asset/e9c4865852b14d1a8621a20b95646113?height=1078&width=1165)<br>

##6.2 深度图获取
本项目使用URP渲染管线，URP渲染管线获取场景深度图的方式如下。

首先分别设置URP管线和摄像机参数：<br>

![微信截图_20230807194437.png#329px #236px](https://km.woa.com/asset/7380eecc1d5e449bae8baad1e33b8fd7?height=674&width=940)![微信截图_20230807194507.png#328px #438px](https://km.woa.com/asset/42e9b0d955b045689467f1c2f922a9d2?height=1276&width=955)<br>

接着自定义一个RenderFeature：GetDepthTextureRenderFeature，用于获取深度图和生成HizMap。在前向渲染通道中，场景深度图在渲染完不透明物体之后，渲染透明物体之前生成。所以GetDepthTextureRenderFeature的执行时期是：RenderPassEvent.BeforeRenderingTransparents。因为此时场景中的不透明物体已经渲染完了，所以生成的Hiz Map只能下一帧使用。当前帧剔除时使用上一帧生成的Hiz Map。上一帧的深度图只是当前帧深度的近似预测。所以，
如果游戏帧率比较低，或者摄像机移动比较快时，会产生瑕疵，如下图。

![瑕疵.gif](https://km.woa.com/asset/fca9d04fe1d44e0b83c7ad50956ddb0e?height=793&width=1723)

另外，由于使用的是上一帧的深度图，理论上深度图在一帧中任何时间生成都可以，不一定非得是BeforeRenderingTransparents。但是在BeforeRenderingTransparents获取深度图的好处是，RenderingTransparents之后的流程中（比如屏幕后处理），如果需要使用深度图，那获取的就是当前帧的深度图。

在GetDepthTextureRenderFeature中，使用如下代码
renderingData.cameraData.renderer.cameraDepthTargetHandle获取深度图。我看过其他人的项目，有些人使用RenderTargetIdentifier CameraDepthTexture = "_CameraDepthTexture"来获取深度图。这个和Unity版本以及URP版本有关。我的项目使用_CameraDepthTexture就得不到深度图。

##6.3 Hiz Map生成
获取到深度图之后，就是生成Hiz Map的步骤了。好多人的实现，是每生成一个Mip就需要Blit一次，如下：

``` 
inline float CalculatorMipmapDepth(float2 uv)
{
	float4 depth;
	float offset = _MainTex_TexelSize.x / 2;
	depth.x = tex2D(_MainTex, uv);
	depth.y = tex2D(_MainTex, uv + float2(0, offset));
	depth.z = tex2D(_MainTex, uv + float2(offset, 0));
	depth.w = tex2D(_MainTex, uv + float2(offset, offset));
#if defined(UNITY_REVERSED_Z)
	return min(min(depth.x, depth.y), min(depth.z, depth.w));
#else
	return max(max(depth.x, depth.y), max(depth.z, depth.w));
#endif
}
```
《天刀》的分享中提到了一种优化方法：<br>

![天刀HizMap.png#500px#auto#center](https://km.woa.com/asset/9aaa1d862a9145348cb06adba5a280ab?height=610&width=1080)<br>

利用ComputeShader的Group Share Buffer和GroupMemoryBarrierWithGroupSync()，来优化流程，减少Blit或者Dispatch的次数。一次Dispatch可以生成4个Mip图。这个方法可在许多场景被应用，比如做高斯模糊时，可以用这个方法来减少Blit的次数。我的项目生成HizMap的方式，就是使用这种思路。
``` 
#pragma kernel BuildHizMap

#pragma multi_compile_local __ _REVERSE_Z

#include "./TerrainFuncUtil.cginc"

Texture2D<float> inputDepthMap; //2488 *1080
uniform float4 inputDepthMapSize; //(2488 ,1080, 4096, 2048)

groupshared float hiz_0[32][16];
groupshared float hiz_1[16][8];
groupshared float hiz_2[8][4];
groupshared float hiz_3[4][2];

RWTexture2D<float> HIZ_MAP_Mip0;//
RWTexture2D<float> HIZ_MAP_Mip1;
RWTexture2D<float> HIZ_MAP_Mip2;
RWTexture2D<float> HIZ_MAP_Mip3;


float GetHizDepth2X2(float depth0, float depth1, float depth2, float depth3)
{
#if _REVERSE_Z
    return min(min(depth0, depth1),min(depth2, depth3));
#else
    return max(max(depth0, depth1), max(depth2, depth3));
#endif
}

[numthreads(32, 16, 1)]
void BuildHizMap(uint3 id : SV_DispatchThreadID, uint3 groupId : SV_GroupID, uint3 idInGroup : SV_GroupThreadID)
{
    //step1: 2488 *1080 -> 4096x2048 -> hiz_ping
    float depth = 0;
    
    uint2 srcXY = floor(id.xy * 1.0 * inputDepthMapSize.xy / inputDepthMapSize.zw); // 2488 *1080 -> 4096x2048
    depth = inputDepthMap.Load(uint3(srcXY, 0));
 
    uint2 pix = uint2(idInGroup.x, idInGroup.y);
    hiz_0[pix.x][pix.y] = depth;//32x16

    GroupMemoryBarrierWithGroupSync();
    //step2: hiz_ping(4096x2048) -> hiz_pang(2048x1024) -> output HIZ_MAP_Mip0;
    
    uint2 pix0, pix1, pix2, pix3;

    pix = idInGroup.xy >> 1;//16x8
    pix0 = pix * 2;
    pix1 = pix * 2 + uint2(0, 1);
    pix2 = pix * 2 + uint2(1, 0);
    pix3 = pix * 2 + uint2(1, 1);
    
    depth = GetHizDepth2X2(hiz_0[pix0.x][pix0.y], hiz_0[pix1.x][pix1.y], hiz_0[pix2.x][pix2.y], hiz_0[pix3.x][pix3.y]);  

    hiz_1[pix.x][pix.y] = depth;//16x8
    HIZ_MAP_Mip0[id.xy>>1] = depth;//2048x1024
    
    GroupMemoryBarrierWithGroupSync();
    //step3: hiz_pang(2048*1024) -> hiz_ping(1024*512) -> output HIZ_MAP_Mip1;
    
    pix = idInGroup.xy>>2;//8x4
    pix0 = pix * 2;
    pix1 = pix * 2 + uint2(0, 1);
    pix2 = pix * 2 + uint2(1, 0);
    pix3 = pix * 2 + uint2(1, 1);
    depth = GetHizDepth2X2(hiz_1[pix0.x][pix0.y], hiz_1[pix1.x][pix1.y], hiz_1[pix2.x][pix2.y], hiz_1[pix3.x][pix3.y]);

    hiz_2[pix.x][pix.y] = depth;//8x4
    HIZ_MAP_Mip1[id.xy >>2] = depth; //1024x512
    
    GroupMemoryBarrierWithGroupSync();
    //step3: hiz_ping(1024x512) -> hiz_pang(512x256) -> output HIZ_MAP_Mip2;
    
    pix = idInGroup.xy >>3;//4x2
    pix0 = pix * 2;
    pix1 = pix * 2 + uint2(0, 1);
    pix2 = pix * 2 + uint2(1, 0);
    pix3 = pix * 2 + uint2(1, 1);
    depth = GetHizDepth2X2(hiz_2[pix0.x][pix0.y], hiz_2[pix1.x][pix1.y], hiz_2[pix2.x][pix2.y], hiz_2[pix3.x][pix3.y]);
    hiz_3[pix.x][pix.y] = depth;//4x2
    HIZ_MAP_Mip2[id.xy >>3] = depth; //512x256
    
    GroupMemoryBarrierWithGroupSync();
    //step4: hiz_pang(1024x512) -> hiz_ping(512x256) -> output HIZ_MAP_Mip3;
    pix = idInGroup.xy >>4;//2x1
    pix0 = pix * 2;
    pix1 = pix * 2 + uint2(0, 1);
    pix2 = pix * 2 + uint2(1, 0);
    pix3 = pix * 2 + uint2(1, 1);
    depth = GetHizDepth2X2(hiz_3[pix0.x][pix0.y], hiz_3[pix1.x][pix1.y], hiz_3[pix2.x][pix2.y], hiz_3[pix3.x][pix3.y]);
    HIZ_MAP_Mip3[id.xy >>4] = depth; //256x128
}
```
原理就是，下一级Mip的计算，依赖上一级Mip的结果。使用Group Shared Buffer来缓存上一级Mip的结果，然后用GroupMemoryBarrierWithGroupSync()来控制同步。只有一个线程组中的所有线程计算完上一级Mip的结果之后，才会继续计算下一级Mip。

利用这种优化结构，一次Dispatch可以生成4个Mip，3次Dispatch，就得到了12级Mip。分辨率是从2048x1024、1024x512 ... 4x2、2x1。

另外不同图形API（OpenGL、DX11、Vulkan、Metal）深度值从远及近有的是1到0，有的是0到1。从远及近0到1这种是反着的，对近处的精度有利，需要在C#侧使用SystemInfo.usesReversedZBuffer判断，或者Shader侧使用宏UNITY_REVERSED_Z来判断。但是我的工程里，发现ComputeShader中UNITY_REVERSED_Z宏无效，只能自己定义KeyWord来区分了。

##6.4 Hiz遮挡剔除
遮挡剔除是先将世界空间的物体包围盒乘以VP矩阵，坐标变换到裁剪空间（Clip Space），然后进行一次透视除法后，包围盒从裁剪空间变换到NDC空间。NDC(Normalized Device Coordinate,标准化设备坐标)。NDC空间中，左右上下是一个[-1,1]的正方形，我们还需要将其变换到UV空间[0,1]。NDC空间深度上根据不同图形API（OpenGL、DX11、Vulkan、Metal）不同，从远及近，有的是[0,1]，有的是[1,0]，有的又是[-1,1]。这里需要根据不同图形API来适配，然后将NDC的深度变换到[0,1]。我称变换完的空间为UVD空间（UV坐标+Depth），三个维度取值范围都是[0,1]，深度远处是1，近处是0。

Unity使用Camera.main.projectionMatrix得到投影矩阵，但是这个是OpenGL标准的，使用GL.GetGPUProjectionMatrix(Camera.main.projectionMatrix)将投影矩阵转换为所在平台的图形API标准。

Unity使用Camera.main.worldToCameraMatrix获得世界World Space到Camera Space的View Matrix。

以上两个矩阵相乘就是VP矩阵，可将点从World Space转换到ClipSpace。Shader中使用以下代码将World Space转换到ClipSpace，然后再继续透视除法，转换到NDC空间，然后再根据图形API变换到UVD空间。
``` 
inline float3 CalPointUVD(GlobalValue gvalue, float4x4 VPMatrix, float3 pos)
{
    float4 clipSpace = mul(VPMatrix, float4(pos, 1));
    float3 ndc = clipSpace.xyz / clipSpace.w;
    
#if SHADER_API_GLES3
    float3 uvd = (ndc + 1) * 0.5;
#else
    float3 uvd;
    uvd.xy = (ndc.xy + 1) * 0.5;
    uvd.z = ndc.z;
#endif
#if _REVERSE_Z
    uvd.z = 1 - uvd.z;
#endif
    return uvd;
}
```
但是一个World Space长方体的包围盒变换到NDC空间时，已经不是长方体了。需要在NDC空间中，重新计算一个AABB包围盒，才能方便之后的运算。如下图：<br>

![ndc.png#auto#500px#center](https://km.woa.com/asset/d72db62eda95497395aceb27b5ee3fc5?height=1353&width=3935)<br>

在新的NDC空间包围盒中，通过对比EF这个面的深度，与EF面占据的所有的屏幕像素的深度。如果EF面的深度大于占据的所有屏幕像素深度，则包围盒被裁剪。只要有一个屏幕像素的深度大于EF面深度，包围盒就被保留。

既然要对比EF面占据的所有的屏幕像素的深度，像素数量影响了算法性能。为了减少需要采样的像素数量，就需要用到HizMap了。利用纹理的Mip，降低采样点数。如下面这3张图，第一张需要采样8x8个像素。如果使用下一级Mip，只需采样4x4个像素。如果使用下两级Mip，只需采样2x2个像素。<br>

![hizcull3.png#225px#225px](https://km.woa.com/asset/52b81e3b02cd4372b4e4e4b44beab115?height=2472&width=2413)![hizcull2.png#225px#225px](https://km.woa.com/asset/90fde8276e214f04ac689c554f8ae577?height=2149&width=2278)![hiz.png#225px#225px](https://km.woa.com/asset/86f283476daf4ef2b72e5ccd3d0bf147?height=1078&width=1165)<br>

一般Hiz采样2x2个像素。那么如何计算得到需要采样的HizMap的Mip级别呢？方法如下公式：

$Mip = Log2(max(Width_{EF}*PixNum_{Mip0}.x,Height_{EF}*PixNum_{Mip0}.y)$

$Width_{EF},Width_{EF}$表示计算EF面的NDC坐标系下的宽和高，取值范围[0,1]。

$PixNum_{Mip0}.x,PixNum_{Mip0}.y$表示HizMap Mip0宽和高分别有多少个像素，本工程是2048x1024.

$max(Width_{EF}*PixNum_{Mip0}.x,Height_{EF}*PixNum_{Mip0}.y)$是计算包围盒EF面宽和高分别占HizMap Mip0的多少个像素，然后取最大值。

再进行一个Log2的计算，就能求出Mip值。

经过上面公式计算出的Mip，能够保证包围盒EF面在该Mip级别中，占据2x2个像素。接着，我们要做的是计算出具体占用哪2x2个像素。具体就是根据EF面4个点的UV坐标，以及HizMap该Mip下的分辨率，求出。然后再采样这4个像素的深度值。与EF面深度对比。代码如下：

``` 
//input a world AABB Box , return a UVD AABB Box
Bound
    CalBoundUVD(GlobalValue
    gvalue, 
    float3 minPos, float3 maxPos)
{
    float3 pos0 = float3(minPos.x, minPos.y, minPos.z);
    float3 pos1 = float3(minPos.x, minPos.y, maxPos.z);
    float3 pos2 = float3(minPos.x, maxPos.y, minPos.z);
    float3 pos3 = float3(maxPos.x, minPos.y, minPos.z);
    float3 pos4 = float3(maxPos.x, maxPos.y, minPos.z);
    float3 pos5 = float3(maxPos.x, minPos.y, maxPos.z);
    float3 pos6 = float3(minPos.x, maxPos.y, maxPos.z);
    float3 pos7 = float3(maxPos.x, maxPos.y, maxPos.z);
    
    float3 uvd0 = CalPointUVD(gvalue, VPMatrix,pos0);
    float3 uvd1 = CalPointUVD(gvalue, VPMatrix, pos1);
    float3 uvd2 = CalPointUVD(gvalue, VPMatrix, pos2);
    float3 uvd3 = CalPointUVD(gvalue, VPMatrix, pos3);
    float3 uvd4 = CalPointUVD(gvalue, VPMatrix, pos4);
    float3 uvd5 = CalPointUVD(gvalue, VPMatrix, pos5);
    float3 uvd6 = CalPointUVD(gvalue, VPMatrix, pos6);
    float3 uvd7 = CalPointUVD(gvalue, VPMatrix, pos7);
    
    float3 minPosUVD = min(min(min(uvd0, uvd1), min(uvd2, uvd3)), min(min(uvd4, uvd5), min(uvd6, uvd7)));
    float3 maxPosUVD = max(max(max(uvd0, uvd1), max(uvd2, uvd3)), max(max(uvd4, uvd5), max(uvd6, uvd7)));
    Bound bound;
    bound.maxPos = maxPosUVD;
    bound.minPos = minPosUVD;
    return bound;
}

//true: avalible
//flase: culled
bool HizCullPoint(int mip, uint2 mapsize_mip,float3 pos)
{
    float2 mip_uv_step = float2(1.0 / mapsize_mip.x, 1.0 / mapsize_mip.y);
    float obj_depth = pos.z;
    //vulkan may be wrong
    uint2 ptXYInMap = uint2(floor(pos.x / mip_uv_step.x), floor(pos.y / mip_uv_step.y));
    float scene_depth = HIZ_MAP.mips[mip][ptXYInMap];
    return CompareDepth(scene_depth, obj_depth);
}
//true: avalible
//flase: culled
bool HizCullBound(GlobalValue gvalue,float3 minPos, float3 maxPos)
{
    float3 pos0 = minPos;
    float3 pos7 = maxPos;
    Bound boundUVD = CalBoundUVD(gvalue, minPos, maxPos);//[0,1]
    float2 objsize = float2(boundUVD.maxPos.x - boundUVD.minPos.x, boundUVD.maxPos.y - boundUVD.minPos.y);//[0,1]
    float objDepth = boundUVD.minPos.z;//[0,1]
    uint2 hizmapsize = gvalue.hizMapSize;
    
    int sample_mip = max(objsize.x * hizmapsize.x, objsize.y * hizmapsize.y);
    sample_mip = clamp(ceil(log2(sample_mip)),0,11);   
    
    float3 boundpos0 = float3(boundUVD.minPos.x , boundUVD.minPos.y , objDepth);
    float3 boundpos1 = float3(boundUVD.minPos.x , boundUVD.maxPos.y , objDepth);
    float3 boundpos2 = float3(boundUVD.maxPos.x , boundUVD.minPos.y , objDepth);
    float3 boundpos3 = float3(boundUVD.maxPos.x , boundUVD.maxPos.y , objDepth);
    uint2 mapsize_mip = uint2(gvalue.hizMapSize.x >> sample_mip, gvalue.hizMapSize.y >> sample_mip);//hiz map resolution of mip
    bool avalible = HizCullPoint(sample_mip, mapsize_mip, boundpos0) 
                    || HizCullPoint(sample_mip, mapsize_mip, boundpos1)
                    || HizCullPoint(sample_mip, mapsize_mip, boundpos2)
                    || HizCullPoint(sample_mip, mapsize_mip, boundpos3);
    return avalible;
}
```
另外我在做这个项目时产生一个疑问，算法要求包围盒EF面的长度刚好覆盖2x2个HizMap的像素。但是长度是2x2，不代表一定能覆盖2x2个像素，也许能覆盖3x3个像素，如下图：<br>

![HizCull4.png#auto#500px#center](https://km.woa.com/asset/2fcfd91c62f84bd4b984b2de0ea61ff3?height=1078&width=2547)<br>

右图蓝色包围盒，占了3x3个像素。所以我认为算法的EF面的长度应该是小于等于1x1个像素，这样才能保证正好占据HizMap的2x2个像素，如右图中红色包围盒。

视锥体剔除后，我们得到了Node列表。通过Node列表，我们得到了Patch列表。将Patch列表中的每个Patch，计算其包围盒，经过上面的Hiz剔除算法，得到最终的剔除完毕的Patch列表。用于之后提交渲染。

``` 
[numthreads(1, 1, 1)]
void HizCull
    (uint3 id : SV_DispatchThreadID,
    uint3 groupId : SV_GroupID, uint3 idInGroup : SV_GroupThreadID)
{
    GlobalValue gvalue = GetGlobalValue(globalValueList);
    NodePatchStruct patch = consumeList.Consume();
    
    float patchSize = GetPatchSizeInLod(gvalue, patch.LOD);
    float2 nodePos = GetNodeCenerPos(gvalue, patch.NodeXY, patch.LOD);
    float2 patchPosInNode = GetPatchPosInNode(gvalue, patch.PatchXY, patch.LOD);
    float2 patchPos = nodePos + patchPosInNode;
    
    uint2 patchWorldXY = patch.NodeXY * gvalue.PATCH_NUM_IN_NODE + patch.PatchXY;
    float2 MinMaxHeight = (MinMaxHeightMap.mips[patch.LOD][patchWorldXY].xy - 0.5) * 2 * gvalue.worldHeightScale;
    
    GetLoadTrans(patch, gvalue, patch.PatchXY);
    
    float3 boundMin = float3(patchPos.x - patchSize * 0.5, MinMaxHeight.x, patchPos.y - patchSize * 0.5);
    float3 boundMax = float3(patchPos.x + patchSize * 0.5, MinMaxHeight.y, patchPos.y + patchSize * 0.5);
    
    bool isHizAvalible = HizCullBound(gvalue, boundMin, boundMax);
    if (isHizAvalible == false)
    {
        return;
    }
    uint currentIndex;
    InterlockedAdd(instanceArgs[1], 1, currentIndex);
    uint2 pixXY;
    pixXY.y = currentIndex * 2 / 512;
    pixXY.x = currentIndex * 2 - pixXY.y * 512;
    
    float4 pix0, pix1;
    pix0.x = patch.NodeXY.x;
    pix0.y = patch.NodeXY.y;
    pix0.z = patch.PatchXY.x * 100 + patch.PatchXY.y;
    pix0.w = patch.LOD;
    pix1 = patch.LodTrans;
    mRenderPatchMap[pixXY] = pix0;
    mRenderPatchMap[pixXY + uint2(1, 0)] = pix1;
}
```
其中，计算Patch包围盒时，我们又用到了4.2章节讲的MinMaxHeightMap。

为了之后处理相邻的不同LOD的Patch之间的接缝问题。我要记录了Node中位于边缘的Patch的邻居的LOD，只需记录邻居LOD比我大的。这里就用到了4.4章节中讲的SectorLODMap了。
``` 
int GetSectorLod(GlobalValue gvalue, int2 sectorXY, int LOD)
{
    int sectornum = GetNodeNumInLod(gvalue, 0);
    if (sectorXY.x < 0 || sectorXY.y < 0 || sectorXY.x >= sectornum || sectorXY.y >= sectornum)
    {
        return LOD;
    }
    int result = round(SectorLODMap[sectorXY] * gvalue.MIN_LOD);
    return result;
}

void GetLoadTrans
    (inout
    NodePatchStruct patch, GlobalValue
    gvalue,
    int2 patchXYInNode)
{
    patch.LodTrans = 0;
    int myLod = patch.LOD;
    int2 scetorXY = patch.NodeXY * (1 << myLod);
    if (patchXYInNode.x == 0)
    {
        patch.LodTrans.x = clamp(GetSectorLod(gvalue, scetorXY + int2(-1, 0), myLod) - myLod, 0, gvalue.MIN_LOD);
    }
    if (patchXYInNode.y == 0)
    {
        patch.LodTrans.y = clamp(GetSectorLod(gvalue, scetorXY + int2(0, -1), myLod) - myLod, 0, gvalue.MIN_LOD);
    }
    if (patchXYInNode.x == gvalue.PATCH_NUM_IN_NODE - 1)
    {
        patch.LodTrans.z = clamp(GetSectorLod(gvalue, scetorXY + int2(1, 0), myLod) - myLod, 0, gvalue.MIN_LOD);
    }
    if (patchXYInNode.y == gvalue.PATCH_NUM_IN_NODE - 1)
    {
        patch.LodTrans.w = clamp(GetSectorLod(gvalue, scetorXY + int2(0, 1), myLod) - myLod, 0, gvalue.MIN_LOD);
    }
}
```

#7 绘制Instance
为了保证平台兼容性，上一章中我用了一张RWTexture2D（mRenderPatchMap）来存储最终Patch结果，而没有使用StructuredBuffer。原因是网上说有些手机的vertex shader中不能使用StructuredBuffer，比如Mali平台的GPU。

将mRenderPatchMap传递给Vertext-Fragment Shader，就可以进入绘制流程了，包含以下步骤。

- 1）根据Patch的坐标计算出Patch在世界空间中的XZ平面的位置
- 2）根据Patch的LOD级别，计算Patch的缩放
- 3）处理相邻的不同LOD的Mesh接缝问题
- 4）根据顶点的世界空间坐标，计算UV
- 5）根据顶点的世界空间坐标采样高度图。

其中步骤1、2、4、5比较简单，我只讲一下步骤3，不同LOD的Mesh接缝问题处理。
如果不处理接缝，当不同LOD的Patch相邻时，就会产生下图中的问题：<br>

![接缝.png#300px#auto#center](https://km.woa.com/asset/9deb75f1860d4f03953746f13922d00e?height=250&width=291)<br>
解决方法就是，偏移顶点，如下图:<br>

![接缝2.png#auto#500px#center](https://km.woa.com/asset/105bd79c63394496b644808ccc0e91b2?height=1143&width=4295)<br>

将左图中红色方框中的顶点偏移到蓝色圆圈的位置。就得到了右图。不同LOD之间的Mesh就没有接缝了。Patch上下左右相邻的Patch的LOD已经在6.4章节的结尾记录在mRenderPatchMap了，计算时直接获取就好了。接缝的处理代码如下：

``` 
inline void FixLODConnectSeam(inout float4 vertex, uint2 PatchXYInNode, uint NodeLOD, uint4 LOADTrans, GlobalValue gValue)
{
    float patchSize = GetPatchSizeInLod(gValue, 0);
    float patchGridSize = patchSize / (gValue.PATCH_GRID_NUM - 1);
    int2 vexIndex = floor((vertex.xz + patchSize * 0.5 + 0.01) / patchGridSize);
    if (vexIndex.x == 0 && LOADTrans.x > 0)
    {
        uint step = 1 << LOADTrans.x;
        uint stepIndex = vexIndex.y % step;
        if (stepIndex != 0)
        {
            vertex.z -= patchGridSize * stepIndex;
        }
        return;
    }
    
    if (vexIndex.y == 0 && LOADTrans.y > 0)
    {
        uint step = 1 << LOADTrans.y;
        uint stepIndex = vexIndex.x % step;
        if (stepIndex != 0)
        {
            vertex.x -= patchGridSize * stepIndex;
        }
        return;
    }
    
    if (vexIndex.x == gValue.PATCH_GRID_NUM - 1 && LOADTrans.z > 0)
    {
        uint step = 1 << LOADTrans.z;
        uint stepIndex = vexIndex.y % step;
        if (stepIndex != 0)
        {
            vertex.z -= patchGridSize * stepIndex;
        }
        return;
    }
    
    if (vexIndex.y == gValue.PATCH_GRID_NUM - 1 && LOADTrans.w > 0)
    {
        uint step = 1 << LOADTrans.w;
        uint stepIndex = vexIndex.x % step;
        if (stepIndex != 0)
        {
            vertex.x -= patchGridSize * stepIndex;
        }
        return;
    }
}
```
到此地形就已经绘制完毕了
#8 总结与讨论
- 1）注意不同图形API的差异
不同图形API（DX11,OpenGL,Vulkan,Metal）,有的是左手坐标系，有的是右手坐标系。有的UV(0,0)位于左下角，有的UV(0,0)位于左上角。深度图有的是远处0近处1;有的是远处1，近处0。NDC空间深度上取值范围也不一样。所以游戏要注意这些差异。
- 2）地形碰撞体
使用了GPUDriven技术后，CPU里就没有地形信息了，该如何做人物与地形的碰撞，这是一个问题。我能想到的是离线预烘焙碰撞体，运行时再加载，但这个会增大内存和包体，不知道有没有更优雅高效的方法。
- 3）阴影的处理
如果使用Unity的默认管线，Unity帮我们处理阴影，这个过程是不需要我们关心的。但是使用GUPDriven管线，
使用了Graphics.DrawMeshInstancedIndirect来绘制地形。Graphics.DrawMeshInstancedIndirect中的物体是摄像机视角下的物体。而渲染阴影的物体是光源视角下的物体。这2个集合并不完全重合。所以，需要我们自己来计算光源视角下，哪些物体会投影阴影到屏幕中。计算直接翻倍了。如果我们再使用了级联阴影等复杂的计算，我们要将视锥体分成几段，计算每段中投影阴影的物体集合，每段对应的集合提交一次Graphics.DrawMeshInstancedIndirect。算法变得麻烦了，性能消耗也变多了。
- 4) GPUDriven可渲染的其他内容
事实上，GPUDriven管线功能开发完毕后，除了可以渲染地形外，植被、建筑等场景物件一般也是一起放进GPUDriven管线里渲染的。场景物件还可以用Cluster来拆分。

#9 源码
https://github.com/lijundacom/LeoGPUDriven.git<br>
Unity版本：2022.3.4f1<br>
URP版本：14.0.8<br>

引用<br>
[1]https://zhuanlan.zhihu.com/p/388844386<br>
[2]https://zhuanlan.zhihu.com/p/396979267<br>
[3]https://zhuanlan.zhihu.com/p/352850047<br>
[4]https://zhuanlan.zhihu.com/p/335325149<br>
[5]https://mp.weixin.qq.com/s/m3e_F5FL3O23FPTGa54wgA

