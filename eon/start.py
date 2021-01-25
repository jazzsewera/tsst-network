import os
import time
import argparse
from pathlib import Path


parser = argparse.ArgumentParser(description="Launch tsst-network")
parser.add_argument("-k", "--kill", action="store_true", help="Kill all CMDs")
parser.add_argument(
    "-b",
    "--build",
    action="store_true",
    help="Build solution with Release configuration",
)
args = parser.parse_args()

project_root = Path(__file__).parent
config_dir = Path("resources/config")
exec_dir = Path("bin/Release/netcoreapp3.1")
log_dir = Path("logs")

cc = Path("CableCloud")
cc_csproj = Path("CableCloud.csproj")
cc_config = Path("CableCloud.xml")
cc_exe = Path("CableCloud.exe")

cn = Path("ClientNetwork")
cn_csproj = Path("ClientNetwork.csproj")
cn_configs = [
    Path("ClientNode1.xml"),
    Path("ClientNode2.xml"),
    Path("ClientNode3.xml"),
    Path("ClientNode4.xml"),
]
cn_exe = Path("ClientNetwork.exe")

nn = Path("NetworkNode")
nn_csproj = Path("NetworkNode.csproj")
nn_configs = [
    Path("NetworkNode1.xml"),
    Path("NetworkNode2.xml"),
    Path("NetworkNode3.xml"),
    Path("NetworkNode4.xml"),
    Path("NetworkNode5.xml"),
    Path("NetworkNode6.xml"),
    Path("NetworkNode7.xml"),
]
nn_exe = Path("NetworkNode.exe")

if os.name == "nt":
    if args.build:
        os.system(
            f'start cmd.exe /k "dotnet build -c Release {(project_root/cc/cc_csproj).absolute()}"'
        )
        os.system(
            f'start cmd.exe /k "dotnet build -c Release {(project_root/nn/nn_csproj).absolute()}"'
        )
        os.system(
            f'start cmd.exe /k "dotnet build -c Release {(project_root/cn/cn_csproj).absolute()}"'
        )
        exit(0)

    if args.kill:
        print("Killing all CMDs")
        os.system("taskkill /F /IM cmd.exe /T")
        exit(0)

    os.system(
        f'start cmd.exe /k "{(project_root/cc/exec_dir/cc_exe).absolute()} -c {(project_root/cc/config_dir/cc_config).absolute()} -l {(project_root/log_dir).absolute()}"'
    )

    time.sleep(2)

    for nn_config in nn_configs:
        os.system(
            f'start cmd.exe /k "{(project_root/nn/exec_dir/nn_exe).absolute()} -c {(project_root/nn/config_dir/nn_config).absolute()} -l {(project_root/log_dir).absolute()}"'
        )

    for cn_config in cn_configs:
        os.system(
            f'start cmd.exe /k "{(project_root/cn/exec_dir/cn_exe).absolute()} -c {(project_root/cn/config_dir/cn_config).absolute()} -l {(project_root/log_dir).absolute()}"'
        )
else:
    if args.build:
        os.system(
            f'{os.getenv("TERM")} -e dotnet build -c Release {(project_root/cc/cc_csproj).absolute()}'
        )
        os.system(
            f'{os.getenv("TERM")} -e dotnet build -c Release {(project_root/nn/nn_csproj).absolute()}'
        )
        os.system(
            f'{os.getenv("TERM")} -e dotnet build -c Release {(project_root/cn/cn_csproj).absolute()}'
        )
        exit(0)

    if args.kill:
        os.system("killall ClientNetwork")
        os.system("killall NetworkNode")
        os.system("killall CableCloud")
        exit(0)

    os.popen(
        f'{os.getenv("TERM")} -e {(project_root/cc/exec_dir/cc).absolute()} -c {(project_root/cc/config_dir/cc_config).absolute()} -l {(project_root/log_dir).absolute()}'
    )

    time.sleep(2)

    for nn_config in nn_configs:
        os.popen(
            f'{os.getenv("TERM")} -e {(project_root/nn/exec_dir/nn).absolute()} -c {(project_root/nn/config_dir/nn_config).absolute()} -l {(project_root/log_dir).absolute()}'
        )

    for cn_config in cn_configs:
        os.popen(
            f'{os.getenv("TERM")} -e {(project_root/cn/exec_dir/cn).absolute()} -c {(project_root/cn/config_dir/cn_config).absolute()} -l {(project_root/log_dir).absolute()}'
        )
