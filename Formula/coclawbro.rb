class Coclawbro < Formula
  desc "Claude Code ↔ GitHub Copilot Proxy"
  homepage "https://github.com/abossard/coclawbro"
  license "MIT"
  version "preview"

  on_macos do
    if Hardware::CPU.arm?
      url "https://github.com/abossard/coclawbro/releases/download/preview/coclawbro-preview-osx-arm64.tar.gz"
      sha256 "96bbc4aa1e84f2013b26f3428185e5629fd2953fc7e3883f91d9cba46135d6b7" # osx-arm64
    else
      url "https://github.com/abossard/coclawbro/releases/download/preview/coclawbro-preview-osx-x64.tar.gz"
      sha256 "12d06cc63ba466e338fe6d56c4a07ae523d39b552cc759cd0828d667f75f0356" # osx-x64
    end
  end

  on_linux do
    if Hardware::CPU.arm?
      if Hardware::CPU.is_64_bit?
        url "https://github.com/abossard/coclawbro/releases/download/preview/coclawbro-preview-linux-arm64.tar.gz"
        sha256 "1f008ae8ff6e01214488e458be61cf134cee393017cb2a1901d3f77da28efee1" # linux-arm64
      else
        url "https://github.com/abossard/coclawbro/releases/download/preview/coclawbro-preview-linux-arm.tar.gz"
        sha256 "be562a5366f07b8e4d078c122fc5d8ae0569bf48c178a9135dd59862ebaf3ef6" # linux-arm
      end
    else
      url "https://github.com/abossard/coclawbro/releases/download/preview/coclawbro-preview-linux-x64.tar.gz"
      sha256 "2399fc4fcae0c84928d4f8e57ba6d0754ed3d166855a76dd827a4458965d119c" # linux-x64
    end
  end

  def install
    bin.install "coclawbro"
  end

  test do
    assert_match "coclawbro", shell_output("#{bin}/coclawbro --version")
  end
end
