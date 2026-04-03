class Coclawbro < Formula
  desc "Claude Code ↔ GitHub Copilot Proxy"
  homepage "https://github.com/abossard/coclawbro"
  license "MIT"
  version "preview"

  on_macos do
    if Hardware::CPU.arm?
      url "https://github.com/abossard/coclawbro/releases/download/preview/coclawbro-preview-osx-arm64.tar.gz"
      sha256 "99524164c4c4300bf8f9bd94ececd97be224ec5f16db2513be6d9e29507d4f31" # osx-arm64
    else
      url "https://github.com/abossard/coclawbro/releases/download/preview/coclawbro-preview-osx-x64.tar.gz"
      sha256 "0e4e938d3bc6d5e4a821ad55089589c6cee9cee5cc4195f9dd61a4de9f9dcfc5" # osx-x64
    end
  end

  on_linux do
    if Hardware::CPU.arm?
      if Hardware::CPU.is_64_bit?
        url "https://github.com/abossard/coclawbro/releases/download/preview/coclawbro-preview-linux-arm64.tar.gz"
        sha256 "6e10dee31cdaefe0c8772756cd23b42864c0b60ac84017d012452929e400c613" # linux-arm64
      else
        url "https://github.com/abossard/coclawbro/releases/download/preview/coclawbro-preview-linux-arm.tar.gz"
        sha256 "6e10dee31cdaefe0c8772756cd23b42864c0b60ac84017d012452929e400c613" # linux-arm
      end
    else
      url "https://github.com/abossard/coclawbro/releases/download/preview/coclawbro-preview-linux-x64.tar.gz"
      sha256 "3383a3b8527f9be95c063d912e0dcefb5959afdc1f441c3d49826387b0f0b433" # linux-x64
    end
  end

  def install
    bin.install "coclawbro"
  end

  test do
    assert_match "coclawbro", shell_output("#{bin}/coclawbro --version")
  end
end
