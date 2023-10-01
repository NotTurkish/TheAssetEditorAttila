﻿using CommonControls.BaseDialogs.ErrorListDialog;
using CommonControls.Common;
using CommonControls.FileTypes.Animation;
using CommonControls.FileTypes.AnimationPack;
using CommonControls.FileTypes.AnimationPack.AnimPackFileTypes.Wh3;
using CommonControls.Services;
using SharpDX.DXGI;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using View3D.Animation;
using static CommonControls.FileTypes.AnimationPack.AnimPackFileTypes.Wh3.AnimationBinEntry;

namespace AnimationEditor.MountAnimationCreator.Services
{
    class BatchProcessorService
    {
        PackFileService _pfs;
        GameSkeleton _riderSkeleton;
        GameSkeleton _mountSkeleton;
        SkeletonAnimationLookUpHelper _skeletonAnimationLookUpHelper;
        MountAnimationGeneratorService _animationGenerator;
        BatchProcessOptions _batchProcessOptions;
        IAnimationBinGenericFormat _mountFragment;
        IAnimationBinGenericFormat _riderFragment;

        uint _animationOutputFormat;
        AnimationPackFile _outAnimPack;
        AnimationBinWh3 _riderOutputBin;

        string _animationPrefix = "new_";
        string _animPackName = "test_tables.animpack";
        string _animBinName = "test_tables.bin";

        public BatchProcessorService(PackFileService pfs, SkeletonAnimationLookUpHelper skeletonAnimationLookUpHelper, MountAnimationGeneratorService animationGenerator, BatchProcessOptions batchProcessOptions, uint animationOutputFormat)
        {
            _pfs = pfs;
            _skeletonAnimationLookUpHelper = skeletonAnimationLookUpHelper;
            _animationGenerator = animationGenerator;
            _batchProcessOptions = batchProcessOptions;
            _animationOutputFormat = animationOutputFormat;

          

            if (_batchProcessOptions != null)
            {
                _animPackName = SaveHelper.EnsureEnding(batchProcessOptions.AnimPackName, ".animpack");
                _animBinName = SaveHelper.EnsureEnding(batchProcessOptions.AnimPackName, "_tables.bin");
            }
        }

        public void Process(IAnimationBinGenericFormat mountFragment, IAnimationBinGenericFormat riderFragment)
        {
            var resultInfo = new ErrorListViewModel.ErrorList();
            _mountFragment = mountFragment;
            _riderFragment = riderFragment;

            _mountSkeleton = LoadSkeleton(_mountFragment.SkeletonName);
            _riderSkeleton = LoadSkeleton(_riderFragment.SkeletonName);

            CreateAnimPackFile();
            CreateFragmentAndAnimations(resultInfo);
            SaveFiles();

            ErrorListWindow.ShowDialog("Mount creation result", resultInfo, false);
        }

        void CreateFragmentAndAnimations(ErrorListViewModel.ErrorList resultInfo)
        {
            // Find all slots that can just be copied over
            foreach (var animationSlot in GetAnimationsThatRequireNoChanges())
                CopyAnimations(animationSlot, resultInfo);

            // Process animations that needs matching
            foreach (var animationSlot in GetMatchedAnimations())
                CreateAnimation(animationSlot.Item1, animationSlot.Item2, resultInfo);
        }

        void CreateAnimation( string riderSlot, string mountSlot, ErrorListViewModel.ErrorList resultInfo)
        {
            // Does the rider have this?
            var riderHasAnimation = _riderFragment.Entries.FirstOrDefault(x => x.SlotName == riderSlot) != null;
            if (riderHasAnimation)
            {
                // Create a copy of the animation fragment entry
                var riderFragment = _riderFragment.Entries.First(x => x.SlotName == riderSlot);
                var mountFragment = _mountFragment.Entries.First(x => x.SlotName == mountSlot);
                
                // Generate new animation
                var riderAnim = LoadAnimation(riderFragment.AnimationFile, _riderSkeleton);
                var mountAnim = LoadAnimation(mountFragment.AnimationFile, _mountSkeleton);
                var savedAnimName = SaveSingleAnim(mountAnim, riderAnim,riderFragment.AnimationFile);


                var newEntry = new CommonControls.FileTypes.AnimationPack.AnimPackFileTypes.Wh3.AnimationBinEntry()
                {
                    AnimationId = (uint)riderFragment.SlotIndex,
                    BlendIn = riderFragment.BlendInTime,
                    SelectionWeight = riderFragment.SelectionWeight,
                    WeaponBools = riderFragment.WeaponBools,
                    Unk = false,
                    AnimationRefs = new List<AnimationRef>()
                    {
                        new AnimationRef()
                        {
                            AnimationFile = savedAnimName,
                            AnimationMetaFile = riderFragment.AnimationFile,
                            AnimationSoundMetaFile = riderFragment.SoundFile
                        }
                    }
                };

                _riderOutputBin.AnimationTableEntries.Add(newEntry);
                resultInfo.Ok(mountSlot, "Matching animation found in rider ("+ riderSlot + "). New animation created");
            }
            else
            {
                // Add an empty fragment entry
                _riderOutputBin.AnimationTableEntries.Add(new CommonControls.FileTypes.AnimationPack.AnimPackFileTypes.Wh3.AnimationBinEntry()
                {
                    AnimationId = (uint)DefaultAnimationSlotTypeHelper.GetfromValue(riderSlot).Id,
                });
            
                resultInfo.Error(mountSlot, "Expected slot missing in  rider (" + riderSlot + "), this need to be resolved!");
            }
        }

        public string SaveSingleAnim(AnimationClip mountAnim, AnimationClip riderAnim,string originalAnimationName)
        {
            var newAnimationName = GenerateNewAnimationName(originalAnimationName, _animationPrefix);

            var newAnimation = _animationGenerator.GenerateMountAnimation(mountAnim, riderAnim);
            // Save the new animation
            var animFile = newAnimation.ConvertToFileFormatMount(_animationGenerator.GetRiderSkeleton(), _pfs.FindFile(originalAnimationName));

            if (_animationOutputFormat != 7)
                animFile.ConvertToVersion(_animationOutputFormat, _skeletonAnimationLookUpHelper, _pfs);

            var bytes = AnimationFile.ConvertToBytes(animFile);
            SaveHelper.Save(_pfs, newAnimationName, null, bytes);

            return newAnimationName;
        }

        void CopyAnimations(string riderSlot, ErrorListViewModel.ErrorList resultInfo)
        {
           var fragmentEntry = _riderFragment.Entries.First(x => x.SlotName == riderSlot);
            var newEntry = new CommonControls.FileTypes.AnimationPack.AnimPackFileTypes.Wh3.AnimationBinEntry()
            {
                AnimationId = (uint)fragmentEntry.SlotIndex,
                BlendIn = fragmentEntry.BlendInTime,
                SelectionWeight = fragmentEntry.SelectionWeight,
                WeaponBools = fragmentEntry.WeaponBools,
                Unk = false,
                AnimationRefs = new List<AnimationRef>()
                {
                    new AnimationRef()
                    {
                        AnimationFile = fragmentEntry.AnimationFile,
                        AnimationMetaFile = fragmentEntry.AnimationFile,
                        AnimationSoundMetaFile = fragmentEntry.SoundFile
                    }
                }
            };

           _riderOutputBin.AnimationTableEntries.Add(newEntry);
           resultInfo.Ok(riderSlot, "Animation copied from rider");
        }

        List<string> GetAnimationsThatRequireNoChanges()
        {
            return _riderFragment.Entries
                   .Where(x => MountAnimationGeneratorService.IsCopyOnlyAnimation(x.SlotName))
                   .Select(x => x.SlotName)
                   .Distinct()
                   .ToList();
        }

        // Animations where rider and mount needs the same amount of frames
        List<(string, string)> GetMatchedAnimations()
        {
            return _mountFragment.Entries
                .Where(x => DefaultAnimationSlotTypeHelper.GetMatchingRiderAnimation(x.SlotName) != null)
                .Select(x => (DefaultAnimationSlotTypeHelper.GetMatchingRiderAnimation(x.SlotName).Value, x.SlotName))
                .Distinct()
                .ToList();
        }

        void CreateAnimPackFile()
        {
            _outAnimPack = new AnimationPackFile();
            _riderOutputBin = new AnimationBinWh3(@"animations/database/battle/bin/" + _animBinName)
            {
                SkeletonName = _riderFragment.SkeletonName,
                Name = Path.GetFileNameWithoutExtension(_animBinName),
                LocomotionGraph = @"animations/locomotion_graphs/entity_locomotion_graph.xml",
                UnknownValue1 = 0,
                MountBin = _mountFragment.Name
            };

            _outAnimPack.AddFile(_riderOutputBin);
        }

        void SaveFiles()
        {
            var bytes = AnimationPackSerializer.ConvertToBytes(_outAnimPack);
            SaveHelper.Save(_pfs, "animations\\database\\battle\\bin\\" + _animPackName, null, bytes);
        }

        AnimationClip LoadAnimation(string path, GameSkeleton skeleton)
        {
            var file = _pfs.FindFile(path);
            var animation = AnimationFile.Create(file);
            return new AnimationClip(animation, skeleton);
        }

        GameSkeleton LoadSkeleton(string skeletonName)
        {
            var skeletonFile = _skeletonAnimationLookUpHelper.GetSkeletonFileFromName(_pfs, skeletonName);          
            return new GameSkeleton(skeletonFile, null);
        }

        string GenerateNewAnimationName(string fullPath, string prefix, int numberId = 0)
        {
            string numberPostFix = "";
            if (numberId != 0)
                numberPostFix = "_" + numberId;

            var potentialName =  Path.GetDirectoryName(fullPath) + "\\" + prefix + numberPostFix + Path.GetFileName(fullPath);
            var fileRef = _pfs.FindFile(potentialName);
            if (fileRef == null)
                return potentialName;
            else
                return GenerateNewAnimationName(fullPath, prefix, numberId+1);
        }
    }
}