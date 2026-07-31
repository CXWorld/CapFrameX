// MIT license 
// 
// Copyright (c) 2017-2024 Advanced Micro Devices, Inc. All rights reserved.
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT.  IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

Texture2D<unorm float4> imageInRGBA : register(t0);
Buffer<uint4> Rectangles               : register(t1);
RWTexture2D<unorm float4> imageOutRGBA : register(u0);

cbuffer Params : register(b0)
{
    uint count;
};
//-------------------------------------------------------------------------------------------------
[numthreads(8, 8, 1)]
void main(uint3 coord : SV_DispatchThreadID)
{
	uint2 idOut = uint2(coord.x, coord.y);

    uint2 imageSize;
    imageOutRGBA.GetDimensions(imageSize.x, imageSize.y);

    if (idOut.x >= imageSize.x || idOut.y >= imageSize.y)
    {
        return;
    }

    float4 color = imageInRGBA[idOut];
    
    for (uint i = 0; i < count; i++)
    {
        uint4 rect = Rectangles[i];

        if (idOut.x >= rect.x && idOut.y >= rect.y && idOut.x < rect.z && idOut.y < rect.w)
        {
            color.x = 1.0f;
            break;
        }
    }
    imageOutRGBA[idOut] = color;
}
