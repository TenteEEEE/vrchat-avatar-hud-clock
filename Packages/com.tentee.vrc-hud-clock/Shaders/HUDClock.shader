Shader "TenteEEEE/HUD Clock"
{
    Properties
    {
        _Color ("Digit Color", Color) = (0.72, 0.96, 1.0, 0.92)
        _ColonColor ("Colon Color", Color) = (0.72, 0.96, 1.0, 0.55)
        _BackColor ("Backing Color", Color) = (0.005, 0.012, 0.018, 0.35)
        _Opacity ("Opacity", Range(0,1)) = 1
        _BackRounding ("Backing Rounding", Float) = 0.12
        _BackPadding ("Backing Padding", Float) = 0.03
        _SegThickness ("Segment Thickness", Float) = 0.10
        _SegGap ("Segment Gap", Float) = 0.04
        _Slant ("Italic Slant", Float) = 0.0
        _ColonSize ("Colon Dot Size", Float) = 0.70
        _ColonSpread ("Colon Dot Spread", Float) = 0.21
        [Toggle] _OneSerif ("Serif On One", Float) = 1
        [Toggle] _ShowSeconds ("Show Seconds", Float) = 1
        [Toggle] _BlinkColon ("Blink Colon", Float) = 0
        _GlowStrength ("Glow Strength", Float) = 0
        _DimSegments ("Unlit Segment Level", Range(0,0.5)) = 0.04
        _MeshStrength ("VFD Mesh Strength", Range(0,1)) = 0.35
        _MeshPitch ("VFD Mesh Pitch", Range(0.01,0.25)) = 0.06
        _MeshWireWidth ("VFD Mesh Wire Width", Range(0.001,0.06)) = 0.018
        [Toggle] _HideInMirror ("Hide In Mirror", Float) = 1
        [Toggle] _HideInCamera ("Hide In Camera", Float) = 1
        _RowMode ("Row Mode (0 clock, 1 event)", Float) = 0
        _EventColor ("Event Digit Color", Color) = (0.72, 0.96, 1.0, 0.92)
        _EventWarnColor ("Event Warn Color", Color) = (1.0, 0.72, 0.30, 0.92)
        _EventIntervalColor ("Event Interval Color", Color) = (0.55, 0.85, 1.0, 0.92)
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Overlay"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
            "VRCFallback" = "Hidden"
        }

        ZTest Always
        ZWrite Off
        Cull Off
        Lighting Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Fog { Mode Off }

            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "UnityCG.cginc"
            #include "Packages/com.vrchat.base/ShaderLibrary/VRCTime.cginc"

            fixed4 _Color;
            fixed4 _ColonColor;
            fixed4 _BackColor;
            float _Opacity;
            float _BackRounding;
            float _BackPadding;
            float _SegThickness;
            float _SegGap;
            float _Slant;
            float _ColonSize;
            float _ColonSpread;
            float _OneSerif;
            float _ShowSeconds;
            float _BlinkColon;
            float _GlowStrength;
            float _DimSegments;
            float _MeshStrength;
            float _MeshPitch;
            float _MeshWireWidth;
            float _HideInMirror;
            float _HideInCamera;
            float _RowMode;
            fixed4 _EventColor;
            fixed4 _EventWarnColor;
            fixed4 _EventIntervalColor;

            // These globals are supplied by VRChat. They are intentionally declared here
            // because they are not part of UnityCG.cginc.
            float _VRChatMirrorMode;
            float _VRChatCameraMode;

            // RotationAlert (a separate world-side gimmick) broadcasts its timer state through
            // these two globals every frame. They are not part of any Unity/VRChat cginc, and
            // they must stay uniform globals rather than material properties: a material
            // property of the same name would shadow the incoming global and always read 0.
            float4 _UdonRotAlertState;
            float4 _UdonRotAlertFlags;

            // These two values are the only layout constants that need changing when the
            // relative cell widths change. The mesh builder mirrors their total width.
            #define HUD_DIGIT_CELL_WIDTH 1.0
            #define HUD_COLON_CELL_WIDTH 0.60

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vert(appdata input)
            {
                v2f output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_OUTPUT(v2f, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.uv = input.uv;
                return output;
            }

            float LayoutWidth()
            {
                return 6.0 * HUD_DIGIT_CELL_WIDTH + 2.0 * HUD_COLON_CELL_WIDTH;
            }

            float CellWidth(int cellIndex)
            {
                return (cellIndex == 2 || cellIndex == 5)
                    ? HUD_COLON_CELL_WIDTH
                    : HUD_DIGIT_CELL_WIDTH;
            }

            void ResolveCell(float uvX, out int cellIndex, out float cellU)
            {
                float x = saturate(uvX) * LayoutWidth();
                float start = 0.0;
                cellIndex = 7;
                cellU = 1.0;

                [unroll]
                for (int i = 0; i < 8; i++)
                {
                    float width = CellWidth(i);
                    if (x < start + width || i == 7)
                    {
                        cellIndex = i;
                        cellU = saturate((x - start) / width);
                        break;
                    }
                    start += width;
                }
            }

            // Signed distance to a rounded rectangle. Negative values are inside.
            float SdRoundBox(float2 p, float2 halfSize, float radius)
            {
                float2 q = abs(p) - halfSize + radius;
                return min(max(q.x, q.y), 0.0) + length(max(q, 0.0)) - radius;
            }

            float CoverageFromDistance(float distanceValue)
            {
                // fwidth makes the edge width track the projected pixel size, keeping
                // the procedural segments readable at different distances/resolutions.
                float antialias = max(fwidth(distanceValue), 0.0001);
                return 1.0 - smoothstep(-antialias, antialias, distanceValue);
            }

            float GlyphCoverageFromDistance(float distanceValue, float antialias)
            {
                // Glyph coordinates wrap from the end of one cell to the start of the
                // next. Taking fwidth(distanceValue) after that wrap lets a 2x2 pixel
                // derivative quad see unrelated glyph distances on either side of a
                // cell boundary, which can briefly turn the inter-cell gap into a bright
                // vertical line. The caller supplies a derivative width measured from
                // the continuous layout UV before cell selection instead.
                return 1.0 - smoothstep(-antialias, antialias, distanceValue);
            }

            // A VFD's control grid is a fine wire mesh in front of the phosphor. Returns how much
            // of the pixel a wire covers, and the mesh's own area average through meshAverage.
            float VfdMeshCoverage(float2 layoutP, float pitch, float wire, out float meshAverage)
            {
                pitch = max(pitch, 0.0001);
                wire = clamp(wire, 0.0, pitch);

                // Area covered by the union of the horizontal and vertical wire families.
                float duty = saturate(wire / pitch);
                meshAverage = saturate(duty + duty - duty * duty);

                float2 cellCoord = frac(layoutP / pitch) - 0.5;
                float2 distanceToWire = abs(cellCoord) * pitch;
                float distanceValue = min(distanceToWire.x, distanceToWire.y) - wire * 0.5;
                float antialias = max(fwidth(distanceValue), 0.00001);
                float coverage = 1.0 - smoothstep(-antialias, antialias, distanceValue);

                // Once a pixel spans an appreciable fraction of the pitch the grid can no longer be
                // resolved, so converge on the uniform dimming it averages out to. Without this the
                // mesh moires and swims whenever the head moves.
                // The larger of the two axes decides whether the grid resolves at all.
                // Summing them double counts the footprint, which fades the mesh away
                // entirely at the size the HUD is actually viewed at.
                float footprint = max(max(fwidth(layoutP.x), fwidth(layoutP.y)), 0.000001);
                float fade = saturate(pitch / (footprint * 3.0) - 1.0);
                return lerp(meshAverage, coverage, fade);
            }

            int DigitSegmentMask(int digit)
            {
                // Bit order: top, upper-right, lower-right, bottom,
                // lower-left, upper-left, middle.
                switch (digit)
                {
                    case 0: return 0x3F;
                    case 1: return 0x06;
                    case 2: return 0x5B;
                    case 3: return 0x4F;
                    case 4: return 0x66;
                    case 5: return 0x6D;
                    case 6: return 0x7D;
                    case 7: return 0x07;
                    case 8: return 0x7F;
                    case 9: return 0x6F;
                    default: return 0;
                }
            }

            // Returns the SDF for one of the seven segments in a unit-height digit cell.
            // The two vertical segments are deliberately shorter than the horizontal
            // segments, leaving a clear LCD-style gap at each corner.
            float SegmentDistance(float2 originalP, int segmentIndex)
            {
                float2 p = originalP;
                p.x += _Slant * p.y;

                float thickness = max(_SegThickness, 0.001);
                float gap = max(_SegGap, 0.0);
                float halfThickness = thickness * 0.5;
                float radius = min(halfThickness, 0.025);
                float horizontalLength = max(0.08, 0.34 - gap * 0.25);
                float offset = 0.34 + gap * 0.5;

                // The vertical segments are derived from the bar geometry rather than
                // given a fixed length: they run from the middle bar's outer edge to the
                // top/bottom bar's inner edge, minus the gap. A hard-coded length makes
                // them overshoot past the horizontal bars and leave stubs on 2/4/5/6/9.
                float barInnerEdge = offset - halfThickness;
                float middleOuterEdge = halfThickness;
                float verticalCenter = (barInnerEdge + middleOuterEdge) * 0.5;
                float verticalLength = max(0.02, (barInnerEdge - middleOuterEdge) * 0.5 - gap * 0.5);
                float2 center;
                float2 halfSize;

                if (segmentIndex == 0)
                {
                    center = float2(0.0, offset);
                    halfSize = float2(horizontalLength, halfThickness);
                }
                else if (segmentIndex == 1)
                {
                    center = float2(offset, verticalCenter);
                    halfSize = float2(halfThickness, verticalLength);
                }
                else if (segmentIndex == 2)
                {
                    center = float2(offset, -verticalCenter);
                    halfSize = float2(halfThickness, verticalLength);
                }
                else if (segmentIndex == 3)
                {
                    center = float2(0.0, -offset);
                    halfSize = float2(horizontalLength, halfThickness);
                }
                else if (segmentIndex == 4)
                {
                    center = float2(-offset, -verticalCenter);
                    halfSize = float2(halfThickness, verticalLength);
                }
                else if (segmentIndex == 5)
                {
                    center = float2(-offset, verticalCenter);
                    halfSize = float2(halfThickness, verticalLength);
                }
                else
                {
                    center = float2(0.0, 0.0);
                    halfSize = float2(horizontalLength, halfThickness);
                }

                return SdRoundBox(p - center, halfSize, radius);
            }

            float DrawDigit(float2 p, int digit, float antialias, out float nearestLitDistance)
            {
                int mask = DigitSegmentMask(digit);
                float litCoverage = 0.0;
                float allCoverage = 0.0;
                nearestLitDistance = 1000.0;

                [unroll]
                for (int segment = 0; segment < 7; segment++)
                {
                    float distanceValue = SegmentDistance(p, segment);
                    float coverage = GlyphCoverageFromDistance(distanceValue, antialias);
                    allCoverage = max(allCoverage, coverage);

                    if ((digit != 1 || _OneSerif <= 0.5) &&
                        (mask & (1 << segment)) != 0)
                    {
                        litCoverage = max(litCoverage, coverage);
                        nearestLitDistance = min(nearestLitDistance, distanceValue);
                    }
                }

                if (digit == 1 && _OneSerif > 0.5)
                {
                    float thickness = max(_SegThickness, 0.001);
                    float halfThickness = thickness * 0.5;
                    float radius = min(halfThickness, 0.025);
                    float gap = max(_SegGap, 0.0);
                    float offset = 0.34 + gap * 0.5;
                    float2 serifP = p;
                    serifP.x += _Slant * serifP.y;

                    // One continuous stroke replaces the two vertical segments so
                    // the serif glyph cannot inherit the LCD middle gap.
                    float strokeDistance = SdRoundBox(
                        serifP - float2(offset, 0.0),
                        float2(halfThickness, offset),
                        radius);

                    // The flag is shorter than the previous version so its diagonal
                    // accent supports the stroke instead of dominating the glyph.
                    float flagAngle = 45.0 * 0.01745329252;
                    float2 flagAxis = float2(cos(flagAngle), sin(flagAngle));
                    float2 flagNormal = float2(-flagAxis.y, flagAxis.x);
                    float2 flagStart = float2(offset, offset);
                    float2 flagEnd = float2(offset - 0.16, offset - 0.16);
                    float2 flagCenter = (flagStart + flagEnd) * 0.5;
                    float flagHalfLength = length(flagEnd - flagStart) * 0.5;
                    float2 flagDelta = serifP - flagCenter;
                    float2x2 flagRotation = float2x2(
                        flagAxis.x, flagAxis.y,
                        flagNormal.x, flagNormal.y);
                    float2 flagFrame = mul(flagRotation, flagDelta);
                    float flagDistance = SdRoundBox(
                        flagFrame,
                        float2(flagHalfLength, halfThickness),
                        radius);

                    // The foot shares the baseline and is left-weighted to mirror the
                    // flag, while its fixed half-width gives the glyph a clean finish.
                    float footCenterX = offset - 0.09;
                    float footHalfWidth = 0.15;
                    float footDistance = SdRoundBox(
                        serifP - float2(footCenterX, -offset),
                        float2(footHalfWidth, halfThickness),
                        radius);

                    float strokeCoverage = GlyphCoverageFromDistance(strokeDistance, antialias);
                    float flagCoverage = GlyphCoverageFromDistance(flagDistance, antialias);
                    float footCoverage = GlyphCoverageFromDistance(footDistance, antialias);
                    litCoverage = max(strokeCoverage, max(flagCoverage, footCoverage));
                    nearestLitDistance = min(
                        min(strokeDistance, flagDistance),
                        footDistance);
                }

                // DimSegments is a direct 0..0.5 coverage level for the unlit figure-8.
                float dimCoverage = allCoverage * _DimSegments;
                return saturate(max(litCoverage, dimCoverage));
            }

            // p must arrive in the SAME units as the digit cells use, i.e. layout-width
            // units. The caller is responsible for that; see the note at the call site.
            float DrawColon(float2 originalP, float visibility, float antialias)
            {
                float2 p = originalP;
                p.x += _Slant * p.y;
                float radius = max(_SegThickness * max(_ColonSize, 0.05), 0.012);
                float spread = max(_ColonSpread, radius);
                float upper = length(p - float2(0.0, spread)) - radius;
                float lower = length(p - float2(0.0, -spread)) - radius;
                return max(
                    GlyphCoverageFromDistance(upper, antialias),
                    GlyphCoverageFromDistance(lower, antialias)) * visibility;
            }

            float4 frag(v2f input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                if (_HideInMirror > 0.5)
                {
                    if (_VRChatMirrorMode != 0) discard;
                }
                if (_HideInCamera > 0.5)
                {
                    if (_VRChatCameraMode != 0) discard;
                }

                bool eventMode = _RowMode > 0.5;
                uint eventPhase = 0u;
                float eventRemaining = 0.0;
                float eventRotIndex = 0.0;
                float eventRotCount = 0.0;
                float eventPaused = 0.0;
                float eventWarnSeconds = 0.0;
                if (eventMode)
                {
                    // RotationAlert's own rule: the globals persist across world changes, so a
                    // zero or stale heartbeat means "no compatible world loaded" rather than
                    // "timer at zero". This discard has to run before any plate/glyph work
                    // below, since that is the only way to also blank the backing plate - the
                    // normal state in worlds that never run RotationAlert.
                    bool heartbeatFresh = _UdonRotAlertFlags.w != 0.0 &&
                        abs(_Time.y - _UdonRotAlertFlags.w) <= 2.0;
                    eventPhase = (uint)round(_UdonRotAlertState.y);
                    if (!heartbeatFresh || eventPhase == 0u) discard;

                    eventRemaining = _UdonRotAlertState.x;
                    eventRotIndex = _UdonRotAlertState.z;
                    eventRotCount = _UdonRotAlertState.w;
                    eventPaused = _UdonRotAlertFlags.x;
                    eventWarnSeconds = _UdonRotAlertFlags.y;
                }

                uint hours;
                uint minutes;
                uint seconds;
                uint milliseconds;
                VRC_GetLocalTime(hours, minutes, seconds, milliseconds);

                // VRChat leaves all time globals at zero in the editor outside play mode.
                // Use a readable fixed preview instead of presenting a blank/broken HUD.
                if (_VRChatTimeUTCUnixSeconds == 0u &&
                    _VRChatTimeNetworkMs == 0u &&
                    _VRChatTimeEncoded1 == 0u &&
                    _VRChatTimeEncoded2 == 0u)
                {
                    hours = 12u;
                    minutes = 34u;
                    seconds = 56u;
                    milliseconds = 0u;
                }

                int cellIndex;
                float cellU;

                // Measure the glyph's screen-space footprint while x is still continuous.
                // All digit and colon local x coordinates use layout-width units, so this
                // single value remains valid after ResolveCell wraps the local coordinate.
                float2 layoutFootprint = fwidth(float2(
                    input.uv.x * LayoutWidth(),
                    input.uv.y));
                float glyphAntialias = max(
                    layoutFootprint.x + (1.0 + abs(_Slant)) * layoutFootprint.y,
                    0.0001);

                ResolveCell(input.uv.x, cellIndex, cellU);
                bool isColon = (cellIndex == 2 || cellIndex == 5);

                // Event-row values, computed only when needed. Cells 0/1 show the rotation
                // index (or, once finished, the rotation count) with leading-zero suppression;
                // cells 3/4 and 6/7 always show mm:ss regardless of _ShowSeconds/_BlinkColon.
                int eventLeftValue = 0;
                bool eventLeftBlank = false;
                int eventMm = 0;
                int eventSs = 0;
                fixed4 eventChosenColor = _EventColor;
                if (eventMode)
                {
                    bool finished = eventPhase == 3u;
                    eventLeftValue = clamp((int)round(finished ? eventRotCount : eventRotIndex), 0, 99);
                    eventLeftBlank = eventLeftValue < 10;

                    // 99:59 is the most the two mm cells can show; clamping keeps ss in 0..59.
                    float secondsLeft = finished ? 0.0 : clamp(ceil(eventRemaining), 0.0, 5999.0);
                    eventMm = clamp((int)floor(secondsLeft / 60.0), 0, 99);
                    eventSs = (int)(secondsLeft - eventMm * 60.0);

                    if (eventPhase == 2u || finished)
                    {
                        eventChosenColor = _EventIntervalColor;
                    }
                    else
                    {
                        bool warn = (eventWarnSeconds > 0.0 && eventRemaining <= eventWarnSeconds) ||
                            eventRemaining <= 60.0;
                        eventChosenColor = warn ? _EventWarnColor : _EventColor;
                    }
                }

                fixed4 glyphColor = eventMode
                    ? eventChosenColor
                    : (isColon ? _ColonColor : _Color);

                float glyphCoverage = 0.0;
                float nearestLitDistance = 1000.0;
                if (isColon)
                {
                    if (eventMode)
                    {
                        // Cell 2 is always dark in event mode; cell 5 is a steady-on
                        // separator instead of the clock's blinking one.
                        if (cellIndex == 5)
                        {
                            glyphCoverage = DrawColon(
                                float2((cellU - 0.5) * HUD_COLON_CELL_WIDTH, input.uv.y - 0.5),
                                1.0,
                                glyphAntialias);
                        }
                    }
                    else
                    {
                        float phase = (float)(milliseconds % 1000u) / 1000.0;
                        float blink = 0.5 + 0.5 * cos(phase * 6.28318530718);
                        float visibility = lerp(1.0, blink, saturate(_BlinkColon));
                        // cellU is normalised within its own cell, so scaling x back into
                        // layout units keeps the dots round; without it they are squashed
                        // to the separator cell width and read as thin strokes next to bars.
                        glyphCoverage = DrawColon(
                            float2((cellU - 0.5) * HUD_COLON_CELL_WIDTH, input.uv.y - 0.5),
                            visibility,
                            glyphAntialias);
                    }
                }
                else if (eventMode && cellIndex == 0 && eventLeftBlank)
                {
                    // Leading-zero suppression on the tens digit of the rotation index/count:
                    // draw nothing rather than the dim unlit figure-8.
                }
                else
                {
                    int digit;
                    if (eventMode)
                    {
                        if (cellIndex == 0) digit = eventLeftValue / 10;
                        else if (cellIndex == 1) digit = eventLeftValue % 10;
                        else if (cellIndex == 3) digit = eventMm / 10;
                        else if (cellIndex == 4) digit = eventMm % 10;
                        else if (cellIndex == 6) digit = eventSs / 10;
                        else digit = eventSs % 10;
                    }
                    else
                    {
                        if (cellIndex == 0) digit = (int)(hours / 10u);
                        else if (cellIndex == 1) digit = (int)(hours % 10u);
                        else if (cellIndex == 3) digit = (int)(minutes / 10u);
                        else if (cellIndex == 4) digit = (int)(minutes % 10u);
                        else if (cellIndex == 6) digit = (int)(seconds / 10u);
                        else digit = (int)(seconds % 10u);
                    }

                    glyphCoverage = DrawDigit(
                        float2(cellU - 0.5, input.uv.y - 0.5),
                        digit,
                        glyphAntialias,
                        nearestLitDistance);
                }

                // Hiding seconds also hides the separator before them, while retaining
                // the fixed eight-cell layout so the quad never changes its geometry. Event
                // mode ignores _ShowSeconds entirely: mm:ss always shows.
                if (!eventMode && cellIndex >= 5 && _ShowSeconds < 0.5)
                {
                    glyphCoverage = 0.0;
                    nearestLitDistance = 1000.0;
                }

                // A paused RotationAlert timer stops changing on its own, so pulse the glyph
                // coverage to make "paused" visibly different from "frozen/broken".
                if (eventMode && eventPaused > 0.5)
                {
                    glyphCoverage *= 0.55 + 0.45 * cos(_Time.y * 3.0);
                }

                float glow = 0.0;
                if (_GlowStrength > 0.0 && nearestLitDistance < 1000.0)
                {
                    float outsideDistance = max(nearestLitDistance, 0.0);
                    glow = saturate(_GlowStrength) * (1.0 - glyphCoverage) * exp(-outsideDistance * 24.0);
                }
                if (_MeshStrength > 0.0)
                {
                    float2 layoutP = float2(input.uv.x * LayoutWidth(), input.uv.y);
                    float meshAverage;
                    float meshCoverage = VfdMeshCoverage(
                        layoutP,
                        _MeshPitch,
                        _MeshWireWidth,
                        meshAverage);
                    glyphCoverage *= 1.0 - saturate(_MeshStrength) * meshCoverage;
                }

                // The selected glyph color's alpha is the overall opacity of the
                // digit/colon, so it scales both glyph and glow coverage.
                float frontAlpha = saturate(glyphCoverage + glow) * saturate(glyphColor.a) * saturate(_Opacity);

                float aspect = LayoutWidth();
                float2 plateP = (input.uv - 0.5) * float2(aspect, 1.0);
                // The padding is expressed in cell-height units on both axes. Scaling it
                // by the aspect would inset the plate ~7x more horizontally than
                // vertically, leaving the outermost digits hanging off the plate.
                float2 plateHalfSize = float2(
                    max(aspect * 0.5 - _BackPadding, 0.01),
                    max(0.5 - _BackPadding, 0.01));
                float plateRadius = min(max(_BackRounding, 0.0), min(plateHalfSize.x, plateHalfSize.y));
                float plateDistance = SdRoundBox(plateP, plateHalfSize, plateRadius);
                float plateAlpha = CoverageFromDistance(plateDistance) * saturate(_BackColor.a) * saturate(_Opacity);

                // Composite the translucent backing plate and digits in premultiplied
                // form, then return ordinary straight-alpha output for the blend state.
                float outputAlpha = frontAlpha + plateAlpha * (1.0 - frontAlpha);
                float3 premultiplied = glyphColor.rgb * frontAlpha +
                    _BackColor.rgb * plateAlpha * (1.0 - frontAlpha);
                float3 outputRgb = premultiplied / max(outputAlpha, 0.0001);
                return float4(outputRgb, outputAlpha);
            }
            ENDCG
        }
    }
}



