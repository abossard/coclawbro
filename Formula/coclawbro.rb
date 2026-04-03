class Coclawbro < Formula
  desc "Claude Code ↔ GitHub Copilot Proxy"
  homepage "https://github.com/abossard/coclawbro"
  license "MIT"
  version "preview"

  on_macos do
    if Hardware::CPU.arm?
      url "https://github.com/abossard/coclawbro/releases/download/preview/coclawbro-preview-osx-arm64.tar.gz"
      sha256 "PLACEHOLDER" # osx-arm64
    else
      url "https://github.com/abossard/coclawbro/releases/download/preview/coclawbro-preview-osx-x64.tar.gz"
      sha256 "PLACEHOLDER" # osx-x64
    end
  end

  on_linux do
    if Hardware::CPU.arm?
      if Hardware::CPU.is_64_bit?
        url "https://github.com/abossard/coclawbro/releases/download/preview/coclawbro-preview-linux-arm64.tar.gz"
        sha256 "PLACEHOLDER" # linux-arm64
      else
        url "https://github.com/abossard/coclawbro/releases/download/preview/coclawbro-preview-linux-arm.tar.gz"
        sha256 "PLACEHOLDER" # linux-arm
      end
    else
      url "https://github.com/abossard/coclawbro/releases/download/preview/coclawbro-preview-linux-x64.tar.gz"
      sha256 "PLACEHOLDER" # linux-x64
    end
  end

  def install
    bin.install "coclawbro"
  end

  test do
    assert_match "coclawbro", shell_output("#{bin}/coclawbro --version")
  end
end
