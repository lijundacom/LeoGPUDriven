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

/* worldPos: ��Ҫ��ɫ�ĵ���������꣨XZƽ�棬��λ���ף�
 * hex_radius ������������������Բ�뾶����λ����
 * HexOriginWorldPos������������ĸ�����Ϊ���������������ԭ�㣬��Ҳ��MaterialIdMap�����½ǵ�����������������ε����ĵ���������ꡣ
 * P0��P1��P2��3��MaterialIdMap���������꣨�������ͱ��ʡ�
 * xy����MaterialIdMap��UV����[0,1]����0��0��λ�����½�
 * z����ɫ��ϵı���,(0,1).P0.z + P1.z + P2.z = 1

 * ��������ϵ����Ϸ����������
 * ��������ϵ�������ε��������ĵ㣬��������6�������ε����ĵ���ɵ���������������ɶ������ϺͶ������µ�2������������ɵ�����
               �������ϵ�Ļ����ǣ�0,1��(-0.5��0.5*sqrt(3))����120�Ƚǣ��������ഹֱ
 * ����������ϵ�������ε��������ĵ㣬��������6�������ε����ĵ���ɵ�����ϵ�������ӳ����񣩡��������ϵ�Ļ����ǣ�0��1����1��0�������ഹֱ
                 �����к�ż���л��ཻ��
 */
void CalPixIndexAndRatio_float(float2 worldPos, float hex_radius, float2 HexOriginWorldPos, float2 matIdMapSize ,out float3 P0,out float3 P1, out float3 P2)
{
    //����ռ�����ϵת������������ϵ
    float2x2 pos2TriCordMatrix = {
            1.0/(sqrt(3)*hex_radius), 1.0/(3*hex_radius),
            0, 2/(3*hex_radius)
        };
    //��������ϵת������������������ϵ
    float2x2 TriCord2UVMatrix = {
        1 , -0.5,
        0 , 1
    };
    
    //����ռ����� ת���� ��������ϵ
    float2 local_pos = worldPos - HexOriginWorldPos;
    float2 triCord = mul(pos2TriCordMatrix, local_pos);
    
    //�����������ϵ�����С�����֡�
    float2 triFrac = frac(triCord);

    //λ�������ĸ���������
    int down = triFrac.x > triFrac.y ? 1 : 0;

    float i = triFrac.x;
    float j = triFrac.y;
    
    //����4������������������꣬�������ϵ�Ļ����ǣ�0,1��(-0.5��0.5*sqrt(3))
    uint triCordMinX = floor(triCord.x);
    uint triCordMinY = floor(triCord.y);
    uint triCordMaxX = triCordMinX + 1;
    uint triCordMaxY = triCordMinY + 1;
    
    //�������������ϵ������,�������ϵ��һ������ż���У������ֱ������
    float2 PixIndex_A = mul(TriCord2UVMatrix, float2(triCordMinX, triCordMinY));
    float2 PixIndex_B = mul(TriCord2UVMatrix, float2(triCordMinX, triCordMaxY));
    float2 PixIndex_C = mul(TriCord2UVMatrix, float2(triCordMaxX, triCordMaxY));
    float2 PixIndex_D = mul(TriCord2UVMatrix, float2(triCordMaxX, triCordMinY));
    
    //�������������꣬yΪ����ʱ����ƫ��
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
    
    //����������������ɫ�ں�ϵ��
    float ratio_A = (1 - down * i - (1 - down) * j);
    float ratio_C = (1-down) * i + down * j;
    float raito_BD = (1 - ratio_A - ratio_C);
    
    

    //����uv���ں�ϵ��
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
    // һ������������������������
    float prime1 = 12.9898;
    float prime2 = 78.233;

    // ��������Ȼ��Ӧ�����Һ���
    float result = sin(dot(uv, float2(prime1, prime2)));

    // �����ӳ�䵽[0, 1]��Χ
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