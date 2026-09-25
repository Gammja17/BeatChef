// 웹빌드 곡 업로드: 브라우저 파일 피커 → Web Audio 디코딩 → Unity로 샘플 전달
mergeInto(LibraryManager.library, {

  BeatChef_OpenFilePicker: function (goNamePtr) {
    var goName = UTF8ToString(goNamePtr);
    var input = document.getElementById('beatchef-file');
    if (!input) {
      input = document.createElement('input');
      input.type = 'file';
      input.accept = 'audio/*,.mp3,.wav,.ogg';
      input.id = 'beatchef-file';
      input.style.display = 'none';
      document.body.appendChild(input);
    }
    input.onchange = function (e) {
      var file = e.target.files[0];
      if (!file) return;
      var name = file.name.replace(/\.[^.]+$/, '');
      SendMessage(goName, 'OnWebSongDecoding', name);
      file.arrayBuffer().then(function (buf) {
        var ctx = new (window.AudioContext || window.webkitAudioContext)();
        ctx.decodeAudioData(buf, function (audio) {
          // 모노 믹스다운 — 게임은 분석/재생 모두 모노면 충분
          var len = audio.length;
          var mono = new Float32Array(len);
          for (var c = 0; c < audio.numberOfChannels; c++) {
            var data = audio.getChannelData(c);
            for (var i = 0; i < len; i++) mono[i] += data[i] / audio.numberOfChannels;
          }
          window.beatchefSamples = mono;
          SendMessage(goName, 'OnWebSongDecoded', name + '|' + audio.sampleRate + '|' + len);
          ctx.close();
        }, function (err) {
          SendMessage(goName, 'OnWebSongError', '디코딩 실패: ' + err);
        });
      }).catch(function (err) {
        SendMessage(goName, 'OnWebSongError', '파일 읽기 실패: ' + err);
      });
    };
    input.value = '';
    input.click();
  },

  BeatChef_CopySamples: function (ptr, length) {
    if (!window.beatchefSamples) return 0;
    var n = Math.min(length, window.beatchefSamples.length);
    HEAPF32.set(window.beatchefSamples.subarray(0, n), ptr >> 2);
    window.beatchefSamples = null;
    return n;
  }
});
