class Coclawbro < Formula
  desc "Claude Code ↔ GitHub Copilot Proxy"
  homepage "https://github.com/abossard/coclawbro"
  license "MIT"
  version "preview"

  on_macos do
    if Hardware::CPU.arm?
      url "https://github.com/abossard/coclawbro/releases/download/preview/coclawbro-preview-osx-arm64.tar.gz"
      sha256 "0797d706aaf671dae52a4cd5e1cd3d2eff3a32688fc0aa61130d3aa66d3decda" # osx-arm64
    else
      url "https://github.com/abossard/coclawbro/releases/download/preview/coclawbro-preview-osx-x64.tar.gz"
      sha256 "d584c5ba0ae5d20d1ab9f664153797b349d4b6182ebf3494d78185ffdb17f3d4" # osx-x64
    end
  end

  on_linux do
    if Hardware::CPU.arm?
      if Hardware::CPU.is_64_bit?
        url "https://github.com/abossard/coclawbro/releases/download/preview/coclawbro-preview-linux-arm64.tar.gz"
        sha256 "a87aad504383438790bdba7e4bab485927c643933735247d58ed33aeb0d09433" # linux-arm64
      else
        url "https://github.com/abossard/coclawbro/releases/download/preview/coclawbro-preview-linux-arm.tar.gz"
        sha256 "dc9b5c6564d893830d05c95fd79c4f810d21a89b26cf6d25cbb8c1a16c66b159" # linux-arm
      end
    else
      url "https://github.com/abossard/coclawbro/releases/download/preview/coclawbro-preview-linux-x64.tar.gz"
      sha256 "8638f3288f152bd7f9cdb8a4aa41c033eb1655a51fb8ba3886b17dde512f0933" # linux-x64
    end
  end

  def install
    bin.install "coclawbro"
  end

  test do
    assert_match "coclawbro", shell_output("#{bin}/coclawbro --version")
  end
end
