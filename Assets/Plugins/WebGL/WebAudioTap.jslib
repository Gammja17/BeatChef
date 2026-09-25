// WebGL에서 Unity가 막아둔 실시간 파형(GetOutputData 미지원)을 Web Audio로 직접 탭.
// destination으로 가는 모든 연결을 AnalyserNode를 거치게 패치해서
// 에디터의 AudioSource.GetOutputData와 동일한 시간영역 파형을 얻는다.
mergeInto(LibraryManager.library, {

  WebAudioTap_Init: function () {
    try {
      var ctx = (typeof WEBAudio !== 'undefined' && WEBAudio.audioContext) ? WEBAudio.audioContext : null;
      if (!ctx || typeof AudioNode === 'undefined') return 0;
      if (Module.__beatchefTap) return 1;

      var an = ctx.createAnalyser();
      an.fftSize = 512;
      an.smoothingTimeConstant = 0;
      an.connect(ctx.destination);

      // 이후의 destination 연결을 전부 analyser 경유로 우회
      var origConnect = AudioNode.prototype.connect;
      AudioNode.prototype.connect = function (dest) {
        if (dest === ctx.destination && this !== an) {
          arguments[0] = an;
        }
        return origConnect.apply(this, arguments);
      };

      Module.__beatchefTap = { an: an, buf: new Float32Array(an.fftSize) };
      return 1;
    } catch (e) {
      return 0;
    }
  },

  WebAudioTap_GetWave: function (ptr, len) {
    var tap = Module.__beatchefTap;
    if (!tap) return 0;
    tap.an.getFloatTimeDomainData(tap.buf);
    var n = Math.min(len, tap.buf.length);
    for (var i = 0; i < n; i++) {
      HEAPF32[(ptr >> 2) + i] = tap.buf[i];
    }
    return n;
  },

  WebAudioTap_GetSpectrum: function (ptr, len) {
    var tap = Module.__beatchefTap;
    if (!tap) return 0;
    if (!tap.fbuf) tap.fbuf = new Float32Array(tap.an.frequencyBinCount);
    tap.an.getFloatFrequencyData(tap.fbuf); // dB 스케일
    var n = Math.min(len, tap.fbuf.length);
    for (var i = 0; i < n; i++) {
      HEAPF32[(ptr >> 2) + i] = tap.fbuf[i];
    }
    return n;
  }
});
