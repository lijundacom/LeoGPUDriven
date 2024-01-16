#ifndef HEX_MESH_CAL_UV
#define HEX_MESH_CAL_UV



float4 _tileScale[256];
int _ColorTintArrayCount = 0;
float4 _ColorTintArray[256];

void MultiplyColorTint_float(float4 oriColor, int textureIndex, out float4 result)
{
    result = textureIndex <_ColorTintArrayCount ? oriColor * _ColorTintArray[textureIndex] : oriColor * float4(1,1,1,1);
}

void MultiplyTileScale_float(float2 uv, int textureIndex,out float2 result)
{
    result = textureIndex <_ColorTintArrayCount ? uv * _tileScale[textureIndex].xy : uv;
}

/* worldPos: 需要着色的点的世界坐标（XZ平面，单位：米）
 * hex_radius ：正六边形网格的外接圆半径，单位：米
 * HexOriginWorldPos：世界坐标的哪个点作为六边形网格坐标的原点，这也是MaterialIdMap的左下角的像素所代表的六边形的中心点的世界坐标。
 * P0，P1，P2：3个MaterialIdMap的像素坐标（整数）和比率。
 * xy：是MaterialIdMap的UV坐标[0,1]。（0，0）位于左下角
 * z是颜色混合的比率,(0,1).P0.z + P1.z + P2.z = 1

 * 世界坐标系。游戏的世界坐标
 * 三角坐标系：六边形的所有中心点，连接相邻6个六边形的中心点组成的网格。这个网格是由顶点向上和顶点向下的2种正三角形组成的网格
               这个坐标系的基底是（0,1）(-0.5，0.5*sqrt(3))。成120度角，并不互相垂直
 * 六边形坐标系：六边形的所有中心点，连接相邻6个六边形的中心点组成的坐标系（不连接成网格）。这个坐标系的基底是（0，1）（1，0），互相垂直
                 奇数行和偶数行互相交错
 */
void CalPixIndexAndRatio_float(float2 worldPos, float hex_radius, float2 HexOriginWorldPos, float2 matIdMapSize ,out float3 P0,out float3 P1, out float3 P2)
{
    //世界空间坐标系转换到三形坐标系
    float2x2 pos2TriCordMatrix = {
            1.0/(sqrt(3)*hex_radius), 1.0/(3*hex_radius),
            0, 2/(3*hex_radius)
        };
    //三角坐标系转换到六边形正交坐标系
    float2x2 TriCord2UVMatrix = {
        1 , -0.5,
        0 , 1
    };
    
    //世界空间坐标 转换到 三形坐标系
    float2 local_pos = worldPos - HexOriginWorldPos;
    float2 triCord = mul(pos2TriCordMatrix, local_pos);
    
    //求出三角坐标系坐标的小数部分。
    float2 triFrac = frac(triCord);

    //位于上下哪个三角形中
    int down = triFrac.x > triFrac.y ? 1 : 0;

    float i = triFrac.x;
    float j = triFrac.y;
    
    //计算4个采样点的三角形坐标，这个坐标系的基底是（0,1）(-0.5，0.5*sqrt(3))
    uint triCordMinX = floor(triCord.x);
    uint triCordMinY = floor(triCord.y);
    uint triCordMaxX = triCordMinX + 1;
    uint triCordMaxY = triCordMinY + 1;
    
    //求出六边形坐标系的坐标,这个坐标系是一个奇数偶数行，交错的直角坐标
    float2 PixIndex_A = mul(TriCord2UVMatrix, float2(triCordMinX, triCordMinY));
    float2 PixIndex_B = mul(TriCord2UVMatrix, float2(triCordMinX, triCordMaxY));
    float2 PixIndex_C = mul(TriCord2UVMatrix, float2(triCordMaxX, triCordMaxY));
    float2 PixIndex_D = mul(TriCord2UVMatrix, float2(triCordMaxX, triCordMinY));
    
    //处理六边形坐标，y为奇数时，的偏移
    if(triCordMinY & 1 == 1)
    {
        PixIndex_A.x = floor(PixIndex_A.x);
        PixIndex_D.x = floor(PixIndex_D.x);
    }
    else
    {
        PixIndex_B.x = floor(PixIndex_B.x);
        PixIndex_C.x = floor(PixIndex_C.x);
    }
    
    //计算三个采样点颜色融合系数
    float ratio_A = (1 - down * i - (1 - down) * j);
    float ratio_C = (1-down) * i + down * j;
    float raito_BD = (1 - ratio_A - ratio_C);
    
    

    //计算uv和融合系数
    float3 result0 = float3((PixIndex_A + float2(0.5, 0.5)) /matIdMapSize, ratio_A);//A
    float3 result1 = float3((PixIndex_C + float2(0.5, 0.5))/matIdMapSize, ratio_C);//C
    float3 result2 = down ==1 ? float3((PixIndex_D + float2(0.5, 0.5)) /matIdMapSize, raito_BD): float3((PixIndex_B + float2(0.5, 0.5)) /matIdMapSize, raito_BD);
    //result0.y = 1- result0.y;
    //result1.y = 1- result1.y;
    //result2.y = 1- result2.y;

    P0 = result0;
    P1 = result1;
    P2 = result2;
}



float random(float2 uv)
{
    // 一个大的质数，用于增加随机性
    float prime1 = 12.9898;
    float prime2 = 78.233;

    // 计算点积，然后应用正弦函数
    float result = sin(dot(uv, float2(prime1, prime2)));

    // 将结果映射到[0, 1]范围
    return frac(result * 43758.5453);
}

void AlphaBlend_float(float4 color0, float4 color1, float4 color2, float ratio0, float ratio1, float ratio2, out float4 outColor)
{
    float total = ratio0 + ratio1 + ratio2;
    outColor = (color0 * ratio0 + color1 * ratio1 + color2 * ratio2)/total;
}

///
float DitherToAlpha(float2 worldPos, float alpha, float offset)
{
	const float dither[64] = {
		0, 32, 8, 40, 2, 34, 10, 42,
		48, 16, 56, 24, 50, 18, 58, 26 ,
		12, 44, 4, 36, 14, 46, 6, 38 ,
		60, 28, 52, 20, 62, 30, 54, 22,
		3, 35, 11, 43, 1, 33, 9, 41,
		51, 19, 59, 27, 49, 17, 57, 25,
		15, 47, 7, 39, 13, 45, 5, 37,
		63, 31, 55, 23, 61, 29, 53, 21 };

	// int xMat = int(uv.x) % 8;
	// int yMat = int(uv.y) % 8;

	int xMat = int(worldPos.x) & 7;
	int yMat = int(worldPos.y) & 7;
    int index = (yMat * 8 + xMat + int(offset)) & 0x3f;
	float limit = (dither[index]) / 64.0;
	return step(limit, alpha);
}

void AlphaTestBlend_float(float2 worldPos ,float3 P0,float3 P1, float3 P2, out float2 uv)
{
    if(DitherToAlpha(worldPos, P0.z, 0) > 0.5)
    {
        uv = P0.xy;
    }
    else if(DitherToAlpha(worldPos, P1.z, 17) > 0.5)
    {
        uv = P1.xy;
    }
    else
    {
        uv = P2.xy;
    }
}

#endif